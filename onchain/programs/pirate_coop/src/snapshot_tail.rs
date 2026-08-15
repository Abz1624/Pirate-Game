use crate::state::{SnapshotAccount, SnapshotPlayer};

pub const SNAPSHOT_HEADER_LEN: usize = 8 + core::mem::size_of::<SnapshotAccount>();
/// Serialized player tail bytes (slot + x + z + heading + hp + level + flags).
pub const SNAPSHOT_PLAYER_PACKED_LEN: usize = 10;
/// Stride between player slots in the snapshot tail (includes struct padding).
pub const SNAPSHOT_PLAYER_LEN: usize = core::mem::size_of::<SnapshotPlayer>();

pub fn spawn_player(slot: usize) -> SnapshotPlayer {
    SnapshotPlayer {
        slot: slot as u8,
        x: ((slot as i16) * 4 - 6).clamp(-20, 20),
        z: 0,
        heading: 0,
        hp_pct: 255,
        level: 1,
        flags: 0,
    }
}

pub fn player_offset(slot: usize) -> usize {
    SNAPSHOT_HEADER_LEN + slot * SNAPSHOT_PLAYER_LEN
}

pub fn read_player(data: &[u8], slot: usize) -> Option<SnapshotPlayer> {
    let off = player_offset(slot);
    if data.len() < off + SNAPSHOT_PLAYER_LEN {
        return None;
    }
    let bytes = &data[off..off + SNAPSHOT_PLAYER_LEN];
    Some(SnapshotPlayer {
        slot: bytes[0],
        x: i16::from_le_bytes([bytes[1], bytes[2]]),
        z: i16::from_le_bytes([bytes[3], bytes[4]]),
        heading: u16::from_le_bytes([bytes[5], bytes[6]]),
        hp_pct: bytes[7],
        level: bytes[8],
        flags: bytes[9],
    })
}

pub fn write_player(data: &mut [u8], slot: usize, player: &SnapshotPlayer) {
    let off = player_offset(slot);
    if data.len() < off + SNAPSHOT_PLAYER_LEN {
        return;
    }
    data[off] = player.slot;
    data[off + 1..off + 3].copy_from_slice(&player.x.to_le_bytes());
    data[off + 3..off + 5].copy_from_slice(&player.z.to_le_bytes());
    data[off + 5..off + 7].copy_from_slice(&player.heading.to_le_bytes());
    data[off + 7] = player.hp_pct;
    data[off + 8] = player.level;
    data[off + 9] = player.flags;
    // Clear padding so decoders using the full stride never read garbage.
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
