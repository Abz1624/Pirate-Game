//! PvPvE gameplay: plunder, respawn, storm, kraken, leveling, draft.

use crate::config::{Element, GameConfig};
use crate::executors::ExecutorContext;
use crate::fix::Fix;
use crate::modes::{
    KrakenState, MatchMode, PLUNDER_DROP_PCT, PLUNDER_KILLER_PCT, ORB_TTL_TICKS,
    KRAKEN_SPAWN_TICK, MATCH_DURATION_TICKS, PVP_MAX_ORBS, PVP_MAX_PLAYERS, RESPAWN_TICKS,
    SPAWN_PROTECTION_TICKS, STORM_END_TICK, STORM_START_TICK,
};
use crate::pools::{EntityId, Vec2};
use crate::snapshot::{GameEvent, GameEventKind};
use crate::status::StatusFlags;
use crate::world::{EnemyBehaviour, EnemyState, MatchPhase, MatchWorld};
use crate::world_state::OrbState;

const XP_PER_MONSTER_KILL: u32 = 15;
const XP_PER_PLAYER_KILL: u32 = 120;
const BANK_PER_MONSTER: u32 = 8;
const BANK_PER_PLAYER_KILL: u32 = 80;
const XP_PER_LEVEL: u32 = 100;

pub fn tick_match_timer(world: &mut MatchWorld, config: &GameConfig) {
    if world.phase() != MatchPhase::Running {
        return;
    }
    world.header_mut().match_timer = world.header_mut().match_timer.saturating_add(1);
    let timer = world.header().match_timer;

    if world.mode() == MatchMode::PvPvE {
        update_storm(world, timer);
        update_kraken(world, config, timer);
        tick_orbs(world);
        tick_respawns(world);
        tick_spawn_protection(world);

        if timer >= MATCH_DURATION_TICKS {
            finalize_placements(world);
            world.set_phase(MatchPhase::Ended);
        }
    }
}

fn update_storm(world: &mut MatchWorld, timer: u32) {
    if timer < STORM_START_TICK {
        world.header_mut().storm_radius_pct = 100;
        return;
    }
    if timer >= STORM_END_TICK {
        world.header_mut().storm_radius_pct = 25;
    } else {
        let progress = (timer - STORM_START_TICK) as u32;
        let span = (STORM_END_TICK - STORM_START_TICK) as u32;
        let shrink = (75 * progress) / span.max(1);
        world.header_mut().storm_radius_pct = (100 - shrink).max(25) as u8;
    }

    let radius_pct = world.header().storm_radius_pct as i32;
    let storm_radius = Fix::from_int(200 * radius_pct / 100);
    let storm_radius_sq = storm_radius.0 as i64 * storm_radius.0 as i64;

    let mut victims: Vec<usize> = Vec::new();
    let cap = world.player_count() as usize;
    for slot in 0..cap {
        let p = &world.players()[slot];
        if !p.active || p.respawn_timer > 0 {
            continue;
        }
        let d_sq = p.position.x.0 as i64 * p.position.x.0 as i64
            + p.position.z.0 as i64 * p.position.z.0 as i64;
        if d_sq > storm_radius_sq {
            victims.push(slot);
        }
    }
    let dmg = ((timer - STORM_START_TICK) / 30 + 1) as u16;
    for slot in victims {
        let actual = world.players_mut()[slot].status.apply_damage(dmg.min(5));
        world.players_mut()[slot].hp = world.players_mut()[slot].hp.saturating_sub(actual);
        if world.players()[slot].hp == 0 {
            on_player_killed(world, None, slot as u8);
        }
    }
}

fn update_kraken(world: &mut MatchWorld, config: &GameConfig, timer: u32) {
    if timer < KRAKEN_SPAWN_TICK {
        return;
    }
    let kraken_state = world.header().kraken_state;
    if kraken_state == KrakenState::None as u8 {
        spawn_kraken(world, config);
    } else if kraken_state == KrakenState::Spawned as u8 {
        tick_kraken_aoe(world);
    }
}

fn spawn_kraken(world: &mut MatchWorld, config: &GameConfig) {
    let tick = world.tick();
    if let Some(eid) = world.enemies_mut().spawn(tick) {
        let max_hp = 5000u16;
        world.enemies_mut().slots[eid as usize] = EnemyState {
            archetype_id: 99,
            behaviour: EnemyBehaviour::Boss,
            position: Vec2::default(),
            hp: max_hp,
            max_hp,
            element: Element::Water,
            is_boss: true,
            ..Default::default()
        };
        world.header_mut().kraken_state = KrakenState::Spawned as u8;
        world.header_mut().kraken_entity = eid;
        world.snapshot.push_event(GameEvent {
            kind: GameEventKind::BossTelegraph,
            tick,
            target: eid,
            ..Default::default()
        });
        let _ = config;
    }
}

fn tick_kraken_aoe(world: &mut MatchWorld) {
    let eid = world.header().kraken_entity;
    if !world.enemies().is_alive(eid) {
        world.header_mut().kraken_state = KrakenState::Dead as u8;
        return;
    }
    let tick = world.tick();
    if tick % 50 != 0 {
        return;
    }
    let pos = world.enemies().get(eid).map(|e| e.position).unwrap_or_default();
    if let Some(aid) = world.areas_mut().spawn(tick) {
        world.areas_mut().slots[aid as usize] = crate::world::AreaState {
            position: pos,
            radius: Fix::from_int(18),
            damage: 25,
            ttl_ticks: 15,
            hits_players: true,
            ..Default::default()
        };
    }
}

pub fn on_enemy_killed(world: &mut MatchWorld, killer_slot: u8, enemy: &EnemyState, eid: EntityId) {
    let slot = killer_slot as usize;
    if slot >= PVP_MAX_PLAYERS {
        return;
    }
    let tick = world.tick();
    if enemy.is_boss {
        world.header_mut().kraken_state = KrakenState::Dead as u8;
        grant_xp_and_bank(world, slot, XP_PER_PLAYER_KILL * 3, BANK_PER_PLAYER_KILL * 3);
        scatter_plunder_orbs(world, enemy.position, world.players()[slot].bank / 2, tick);
        world.snapshot.push_event(GameEvent {
            kind: GameEventKind::Death,
            tick,
            actor: slot as u16,
            target: eid,
            value: 5000,
            ..Default::default()
        });
        return;
    }
    grant_xp_and_bank(world, slot, XP_PER_MONSTER_KILL, BANK_PER_MONSTER);
    world.players_mut()[slot].kills += 1;
}

use crate::world::PlayerState;

pub fn on_player_killed(world: &mut MatchWorld, killer_slot: Option<u8>, victim_slot: u8) {
    let tick = world.tick();
    let victim = victim_slot as usize;
    if victim >= PVP_MAX_PLAYERS {
        return;
    }
    let bank = world.players()[victim].bank;
    let drop = (bank * PLUNDER_DROP_PCT as u32) / 100;
    let killer_share = (drop * PLUNDER_KILLER_PCT as u32) / 100;
    let scatter = drop.saturating_sub(killer_share);

    world.players_mut()[victim].bank = bank.saturating_sub(drop);
    world.players_mut()[victim].respawn_timer = RESPAWN_TICKS;
    world.players_mut()[victim].hp = 0;
    world.players_mut()[victim].status.flags.insert(StatusFlags::DOWNED);

    if let Some(killer) = killer_slot {
        let k = killer as usize;
        if k < PVP_MAX_PLAYERS {
            world.players_mut()[k].bank = world.players_mut()[k].bank.saturating_add(killer_share);
            world.players_mut()[k].player_kills += 1;
            grant_xp_and_bank(world, k, XP_PER_PLAYER_KILL, BANK_PER_PLAYER_KILL);
        }
    }

    let pos = world.players()[victim].position;
    scatter_plunder_orbs(world, pos, scatter, tick);

    world.snapshot.push_event(GameEvent {
        kind: GameEventKind::PlayerKill,
        tick,
        actor: killer_slot.unwrap_or(255) as u16,
        target: victim as u16,
        value: drop.min(65535) as u16,
        ..Default::default()
    });
}

pub fn grant_xp_and_bank(world: &mut MatchWorld, slot: usize, xp: u32, bank: u32) {
    if slot >= PVP_MAX_PLAYERS {
        return;
    }
    world.players_mut()[slot].xp = world.players_mut()[slot].xp.saturating_add(xp);
    world.players_mut()[slot].bank = world.players_mut()[slot].bank.saturating_add(bank);
    world.players_mut()[slot].gold = world.players_mut()[slot].gold.saturating_add(bank);
    while world.players_mut()[slot].xp >= XP_PER_LEVEL * world.players()[slot].level as u32 {
        world.players_mut()[slot].xp -= XP_PER_LEVEL * world.players()[slot].level as u32;
        world.players_mut()[slot].level = world.players_mut()[slot].level.saturating_add(1);
        world.players_mut()[slot].level_ups_pending =
            world.players_mut()[slot].level_ups_pending.saturating_add(1);
        generate_draft_offers(world, slot);
        let level = world.players()[slot].level;
        let tick = world.tick();
        world.snapshot.push_event(GameEvent {
            kind: GameEventKind::LevelUp,
            tick,
            actor: slot as u16,
            value: level as u16,
            ..Default::default()
        });
    }
}

pub fn generate_draft_offers(world: &mut MatchWorld, slot: usize) {
    let mut prng = world.prng();
    let offers = [
        (prng.next_u32() % 6 + 1) as u8,
        (prng.next_u32() % 6 + 1) as u8,
        (prng.next_u32() % 6 + 1) as u8,
    ];
    world.players_mut()[slot].draft_offers = offers;
    world.set_prng(prng);
}

pub fn apply_draft_pick(world: &mut MatchWorld, slot: u8, card_index: u8) -> bool {
    let s = slot as usize;
    if s >= PVP_MAX_PLAYERS || card_index > 2 {
        return false;
    }
    let p = &mut world.players_mut()[s];
    if p.level_ups_pending == 0 {
        return false;
    }
    let card_id = p.draft_offers[card_index as usize];
    if card_id == 0 {
        return false;
    }
    for ws in p.weapon_slots.iter_mut() {
        if ws.weapon_id == 0 {
            ws.weapon_id = card_id;
            ws.level = 1;
            break;
        } else if ws.weapon_id == card_id {
            ws.level = ws.level.saturating_add(1);
            break;
        }
    }
    p.level_ups_pending = p.level_ups_pending.saturating_sub(1);
    p.draft_offers = [0; 3];
    true
}

fn scatter_plunder_orbs(world: &mut MatchWorld, center: Vec2, total: u32, tick: u32) {
    if total == 0 {
        return;
    }
    let mut prng = world.prng();
    let orb_count = 4.min(PVP_MAX_ORBS);
    let per_orb = total / orb_count as u32;
    for i in 0..orb_count {
        if let Some(oid) = world.orbs_mut().spawn(tick) {
            let angle = prng.next_u32() % 1024;
            let (sx, sz) = crate::trig::sin_cos(angle as u16);
            let dist = Fix::from_int(prng.range_i32(3, 12));
            world.orbs_mut().slots[oid as usize] = OrbState {
                x: (center.x + sx.mul_fix(dist)).to_int() as i16,
                z: (center.z + sz.mul_fix(dist)).to_int() as i16,
                value: per_orb,
                ttl: ORB_TTL_TICKS,
                owner_slot: 255,
                _pad: 0,
            };
        }
        let _ = i;
    }
    world.set_prng(prng);
}

fn tick_orbs(world: &mut MatchWorld) {
    let pickup_radius_sq = Fix::from_int(4).0 as i64 * Fix::from_int(4).0 as i64;
    let mut collected: Vec<(EntityId, u32, u8)> = Vec::new();
    world.orbs().for_each_alive(|oid, orb| {
        for (slot, p) in world.players().iter().enumerate() {
            if !p.active || p.respawn_timer > 0 {
                continue;
            }
            let dx = p.position.x.to_int() as i64 - orb.x as i64;
            let dz = p.position.z.to_int() as i64 - orb.z as i64;
            if dx * dx + dz * dz <= pickup_radius_sq {
                collected.push((oid, orb.value, slot as u8));
                break;
            }
        }
    });
    for (oid, value, slot) in collected {
        world.orbs_mut().despawn(oid);
        let s = slot as usize;
        if s < PVP_MAX_PLAYERS {
            world.players_mut()[s].bank = world.players_mut()[s].bank.saturating_add(value);
            world.players_mut()[s].gold = world.players_mut()[s].gold.saturating_add(value);
        }
    }
    let mut to_despawn = Vec::new();
    world.orbs().for_each_alive(|oid, orb| {
        if orb.ttl <= 1 {
            to_despawn.push(oid);
        }
    });
    for oid in to_despawn {
        world.orbs_mut().despawn(oid);
    }
    world.orbs_mut().for_each_alive_mut(|_, orb| {
        orb.ttl = orb.ttl.saturating_sub(1);
    });
}

fn tick_respawns(world: &mut MatchWorld) {
    let cap = world.player_count() as usize;
    for slot in 0..cap {
        let p = &mut world.players_mut()[slot];
        if p.respawn_timer == 0 {
            continue;
        }
        p.respawn_timer = p.respawn_timer.saturating_sub(1);
        if p.respawn_timer == 0 {
            p.hp = p.max_hp;
            p.status.flags.remove(StatusFlags::DOWNED);
            p.spawn_protection = SPAWN_PROTECTION_TICKS;
            let angle = (slot as u32 * 256 / cap.max(1) as u32) as u16;
            let (sx, sz) = crate::trig::sin_cos(angle);
            let dist = Fix::from_int(40);
            p.position = Vec2::new(sx.mul_fix(dist), sz.mul_fix(dist));
            world.snapshot.push_event(GameEvent {
                kind: GameEventKind::Revive,
                tick: world.tick(),
                actor: slot as u16,
                ..Default::default()
            });
        }
    }
}

fn tick_spawn_protection(world: &mut MatchWorld) {
    for p in world.players_mut().iter_mut() {
        if p.spawn_protection > 0 {
            p.spawn_protection = p.spawn_protection.saturating_sub(1);
            if p.spawn_protection > 0 {
                p.status.flags.insert(StatusFlags::INVULN);
            } else {
                p.status.flags.remove(StatusFlags::INVULN);
            }
        }
    }
}

fn finalize_placements(world: &mut MatchWorld) {
    let mut rankings: Vec<(usize, u32)> = world
        .players()
        .iter()
        .enumerate()
        .filter(|(_, p)| p.active)
        .map(|(i, p)| (i, p.bank))
        .collect();
    rankings.sort_by(|a, b| b.1.cmp(&a.1));
    for (place, (slot, _)) in rankings.iter().enumerate() {
        world.players_mut()[*slot].placement = (place + 1) as u8;
    }
}

pub fn player_is_targetable(p: &PlayerState) -> bool {
    p.active && p.respawn_timer == 0 && p.hp > 0 && !p.status.flags.contains(StatusFlags::DOWNED)
}
