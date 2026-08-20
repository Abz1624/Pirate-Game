use crate::state::{SnapshotAccount, SnapshotEnemy, SnapshotPlayer, SnapshotProjectile, EVENT_RING, SNAPSHOT_ORB_PACKED_LEN};
use pirate_sim::snapshot::{hp_pct, quantize_pos, Snapshot};
use pirate_sim::status::StatusFlags;
use pirate_sim::WorldState;

pub const SNAPSHOT_HEADER_LEN: usize = 8 + core::mem::size_of::<SnapshotAccount>();
/// Serialized player tail bytes (slot + x + z + heading + hp + level + flags + bank + kills).
pub const SNAPSHOT_PLAYER_PACKED_LEN: usize = 16;
/// Stride between player slots in the snapshot tail (includes struct padding).
pub const SNAPSHOT_PLAYER_LEN: usize = core::mem::size_of::<SnapshotPlayer>();

pub fn spawn_player(slot: usize) -> SnapshotPlayer {
    let (x, z) = pirate_sim::world_state::spawn_xy_for_slot(slot, 2);
    SnapshotPlayer {
        slot: slot as u8,
        x: x as i16,
        z: z as i16,
        heading: 0,
        hp_pct: 255,
        level: 1,
        flags: 0,
        bank: 0,
        kills: 0,
    }
}

pub fn player_offset(slot: usize) -> usize {
    SNAPSHOT_HEADER_LEN + slot * SNAPSHOT_PLAYER_LEN
}

pub fn read_player(data: &[u8], slot: usize) -> Option<SnapshotPlayer> {
    let off = player_offset(slot);
    if data.len() < off + SNAPSHOT_PLAYER_PACKED_LEN {
        return None;
    }
    let bytes = &data[off..off + SNAPSHOT_PLAYER_PACKED_LEN];
    Some(SnapshotPlayer {
        slot: bytes[0],
        x: i16::from_le_bytes([bytes[1], bytes[2]]),
        z: i16::from_le_bytes([bytes[3], bytes[4]]),
        heading: u16::from_le_bytes([bytes[5], bytes[6]]),
        hp_pct: bytes[7],
        level: bytes[8],
        flags: bytes[9],
        bank: u32::from_le_bytes([bytes[10], bytes[11], bytes[12], bytes[13]]),
        kills: u16::from_le_bytes([bytes[14], bytes[15]]),
    })
}

pub fn write_player(data: &mut [u8], slot: usize, player: &SnapshotPlayer) {
    let off = player_offset(slot);
    if data.len() < off + SNAPSHOT_PLAYER_PACKED_LEN {
        return;
    }
    data[off] = player.slot;
    data[off + 1..off + 3].copy_from_slice(&player.x.to_le_bytes());
    data[off + 3..off + 5].copy_from_slice(&player.z.to_le_bytes());
    data[off + 5..off + 7].copy_from_slice(&player.heading.to_le_bytes());
    data[off + 7] = player.hp_pct;
    data[off + 8] = player.level;
    data[off + 9] = player.flags;
    data[off + 10..off + 14].copy_from_slice(&player.bank.to_le_bytes());
    data[off + 14..off + 16].copy_from_slice(&player.kills.to_le_bytes());
    if SNAPSHOT_PLAYER_LEN > SNAPSHOT_PLAYER_PACKED_LEN {
        data[off + SNAPSHOT_PLAYER_PACKED_LEN..off + SNAPSHOT_PLAYER_LEN].fill(0);
    }
}

pub fn header_mut(data: &mut [u8]) -> Option<&mut SnapshotAccount> {
    if data.len() < SNAPSHOT_HEADER_LEN {
        return None;
    }
    let header = &mut data[8..SNAPSHOT_HEADER_LEN];
    Some(unsafe { &mut *(header.as_mut_ptr() as *mut SnapshotAccount) })
}

pub fn write_snapshot_from_sim(
    data: &mut [u8],
    snap: &Snapshot,
    world: &WorldState,
    player_count: u8,
) {
    if let Some(h) = header_mut(data) {
        h.tick = snap.tick;
        h.player_count = player_count;
        h.enemy_count = snap.enemies.len().min(255) as u8;
        h.projectile_count = snap.enemy_projectiles.len().min(255) as u8;
        h.storm_radius_pct = world.header.storm_radius_pct;
        h.match_timer_sec = (world.header.match_timer / 10) as u16;
        h.event_head = snap.event_head;
    }
    for i in 0..player_count as usize {
        if i < snap.players.len() {
            let p = snap.players[i];
            let populated = p.hp_pct > 0 || p.x != 0 || p.z != 0 || p.heading != 0;
            if populated {
                write_player(data, i, &SnapshotPlayer {
                    slot: i as u8,
                    x: p.x,
                    z: p.z,
                    heading: p.heading,
                    hp_pct: p.hp_pct,
                    level: p.level,
                    flags: if p.downed { 1 } else { 0 },
                    bank: p.bank,
                    kills: p.kills,
                });
                continue;
            }
        }
        if i < world.players.len() {
            let wp = &world.players[i];
            write_player(data, i, &SnapshotPlayer {
                slot: i as u8,
                x: quantize_pos(wp.position.x),
                z: quantize_pos(wp.position.z),
                heading: wp.heading,
                hp_pct: hp_pct(wp.hp, wp.max_hp),
                level: wp.level,
                flags: if wp.status.flags.contains(StatusFlags::DOWNED) {
                    1
                } else {
                    0
                },
                bank: wp.bank,
                kills: wp.player_kills,
            });
        }
    }
    let mut offset = SNAPSHOT_HEADER_LEN + player_count as usize * SNAPSHOT_PLAYER_LEN;
    for e in &snap.enemies {
        if offset + 10 > data.len() {
            break;
        }
        data[offset..offset + 2].copy_from_slice(&e.id.to_le_bytes());
        data[offset + 2] = e.archetype;
        data[offset + 3..offset + 5].copy_from_slice(&e.x.to_le_bytes());
        data[offset + 5..offset + 7].copy_from_slice(&e.z.to_le_bytes());
        data[offset + 7] = e.hp_pct;
        data[offset + 8] = e.element as u8;
        offset += 10;
    }
    for proj in &snap.enemy_projectiles {
        if offset + 10 > data.len() {
            break;
        }
        data[offset..offset + 2].copy_from_slice(&proj.id.to_le_bytes());
        data[offset + 2..offset + 4].copy_from_slice(&proj.x.to_le_bytes());
        data[offset + 4..offset + 6].copy_from_slice(&proj.z.to_le_bytes());
        data[offset + 6..offset + 8].copy_from_slice(&proj.vx.to_le_bytes());
        data[offset + 8..offset + 10].copy_from_slice(&proj.vz.to_le_bytes());
        offset += 10;
    }

    let mut orb_count = 0u8;
    world.orbs.for_each_alive(|oid, orb| {
        if offset + SNAPSHOT_ORB_PACKED_LEN > data.len() || orb_count >= 255 {
            return;
        }
        data[offset..offset + 2].copy_from_slice(&(oid as u16).to_le_bytes());
        data[offset + 2..offset + 4].copy_from_slice(&orb.x.to_le_bytes());
        data[offset + 4..offset + 6].copy_from_slice(&orb.z.to_le_bytes());
        data[offset + 6..offset + 10].copy_from_slice(&orb.value.to_le_bytes());
        offset += SNAPSHOT_ORB_PACKED_LEN;
        orb_count += 1;
    });
    if let Some(h) = header_mut(data) {
        h.orb_count = orb_count;
    }

    for i in 0..EVENT_RING {
        if offset + 16 > data.len() {
            break;
        }
        let ev = snap.events.get(i).copied().unwrap_or_default();
        data[offset] = ev.kind as u8;
        data[offset + 1..offset + 5].copy_from_slice(&ev.tick.to_le_bytes());
        data[offset + 5..offset + 7].copy_from_slice(&ev.actor.to_le_bytes());
        data[offset + 7..offset + 9].copy_from_slice(&ev.target.to_le_bytes());
        data[offset + 9..offset + 11].copy_from_slice(&ev.x.to_le_bytes());
        data[offset + 11..offset + 13].copy_from_slice(&ev.z.to_le_bytes());
        data[offset + 13..offset + 15].copy_from_slice(&ev.value.to_le_bytes());
        offset += 16;
    }
}
