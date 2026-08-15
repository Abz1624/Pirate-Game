//! Match world state.

use crate::config::{Element, GameConfig};
use crate::fix::Fix;
use crate::grid::SpatialGrid;
use crate::pools::{EntityId, EntityKind, EntityPool, Vec2};
use crate::prng::Prng;
use crate::snapshot::Snapshot;
use crate::status::StatusState;
use crate::{ARENA_RADIUS, MAX_AREAS, MAX_ENEMIES, MAX_PLAYERS, MAX_PROJECTILES};

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum MatchPhase {
    #[default]
    Lobby = 0,
    Running = 1,
    Ended = 2,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct ShipInput {
    pub throttle: i8,
    pub steer: i8,
    pub buttons: u8,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct WeaponSlot {
    pub weapon_id: u8,
    pub level: u8,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct PassiveSlot {
    pub passive_id: u8,
    pub level: u8,
}

#[derive(Clone, Debug)]
pub struct PlayerState {
    pub active: bool,
    pub wallet_hash: u64,
    pub position: Vec2,
    pub velocity: Vec2,
    pub heading: u16,
    pub hp: u16,
    pub max_hp: u16,
    pub xp: u32,
    pub level: u8,
    pub gold: u32,
    pub kills: u32,
    pub damage_dealt: u32,
    pub revives: u16,
    pub weapon_slots: [WeaponSlot; 6],
    pub passive_slots: [PassiveSlot; 6],
    pub weapon_cooldowns: [u8; 6],
    pub status: StatusState,
    pub downed_timer: u16,
    pub ready: bool,
    pub hull_id: u8,
    pub max_speed: Fix,
    pub turn_rate: Fix,
    pub accel: Fix,
    pub water_drag: Fix,
}

impl Default for PlayerState {
    fn default() -> Self {
        Self {
            active: false,
            wallet_hash: 0,
            position: Vec2::default(),
            velocity: Vec2::default(),
            heading: 0,
            hp: 100,
            max_hp: 100,
            xp: 0,
            level: 0,
            gold: 0,
            kills: 0,
            damage_dealt: 0,
            revives: 0,
            weapon_slots: [WeaponSlot {
                weapon_id: 1,
                level: 1,
            }; 6],
            passive_slots: [PassiveSlot::default(); 6],
            weapon_cooldowns: [0; 6],
            status: StatusState::default(),
            downed_timer: 0,
            ready: false,
            hull_id: 0,
            max_speed: Fix::from_int(50),
            turn_rate: Fix::from_int(800) / Fix::from_int(1024),
            accel: Fix::from_int(15),
            water_drag: Fix::from_int(3),
        }
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum EnemyBehaviour {
    #[default]
    Swarmer = 0,
    Charger = 1,
    Ranged = 2,
    Orbiter = 3,
    Bruiser = 4,
    Elite = 5,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct EnemyState {
    pub archetype_id: u8,
    pub behaviour: EnemyBehaviour,
    pub position: Vec2,
    pub velocity: Vec2,
    pub heading: u16,
    pub hp: u16,
    pub max_hp: u16,
    pub element: Element,
    pub status: StatusState,
    pub state_timer: u16,
    pub target_slot: u8,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct ProjectileState {
    pub owner_slot: u8,
    pub weapon_id: u8,
    pub position: Vec2,
    pub velocity: Vec2,
    pub damage: u16,
    pub pierce_left: u8,
    pub range_left: Fix,
    pub element: Element,
    pub from_player: bool,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct AreaState {
    pub position: Vec2,
    pub radius: Fix,
    pub damage: u16,
    pub element: Element,
    pub ttl_ticks: u16,
    pub owner_slot: u8,
}

pub struct MatchWorld {
    pub tick: u32,
    pub seed: u64,
    pub tide: u8,
    pub capacity: u8,
    pub player_count: u8,
    pub phase: MatchPhase,
    pub prng: Prng,
    pub players: Vec<PlayerState>,
    pub enemies: EntityPool<EnemyState>,
    pub projectiles: EntityPool<ProjectileState>,
    pub areas: EntityPool<AreaState>,
    pub grid: SpatialGrid,
    pub snapshot: Snapshot,
    pub pending_inputs: Vec<ShipInput>,
    pub spawn_accumulator: Fix,
    pub wave_index: u16,
}

impl MatchWorld {
    pub fn new(seed: u64, tide: u8, capacity: u8, config: &GameConfig) -> Self {
        let cap = capacity.min(MAX_PLAYERS as u8) as usize;
        let max_enemies = config
            .tides
            .get(tide.saturating_sub(1) as usize)
            .map(|t| t.max_enemies as usize)
            .unwrap_or(60)
            .min(MAX_ENEMIES);
        Self {
            tick: 0,
            seed,
            tide,
            capacity: capacity.min(MAX_PLAYERS as u8),
            player_count: 0,
            phase: MatchPhase::Lobby,
            prng: Prng::new(seed),
            players: vec![PlayerState::default(); cap],
            enemies: EntityPool::with_capacity(max_enemies),
            projectiles: EntityPool::with_capacity(MAX_PROJECTILES),
            areas: EntityPool::with_capacity(MAX_AREAS),
            grid: SpatialGrid::default(),
            snapshot: Snapshot::with_capacity(cap, max_enemies),
            pending_inputs: vec![ShipInput::default(); cap],
            spawn_accumulator: Fix::ZERO,
            wave_index: 0,
        }
    }

    pub fn join_player(&mut self, slot: u8) -> bool {
        let s = slot as usize;
        if s >= self.players.len() || self.players[s].active {
            return false;
        }
        self.players[s].active = true;
        self.players[s].position = Vec2::new(
            Fix::from_int(self.prng.range_i32(-20, 20)),
            Fix::from_int(self.prng.range_i32(-20, 20)),
        );
        self.player_count += 1;
        true
    }

    pub fn state_hash(&self) -> u64 {
        let mut h = self.tick as u64 ^ self.seed;
        for (i, p) in self.players.iter().enumerate() {
            if p.active {
                h ^= (i as u64 + 1).wrapping_mul(p.position.x.0 as u64);
                h ^= p.hp as u64;
            }
        }
        h
    }

    pub fn clamp_to_arena(&self, pos: &mut Vec2) {
        clamp_position_to_arena(pos);
    }
}

pub fn clamp_position_to_arena(pos: &mut Vec2) {
    let r_sq = (ARENA_RADIUS.0 as i64) * (ARENA_RADIUS.0 as i64);
    let d_sq = pos.x.0 as i64 * pos.x.0 as i64 + pos.z.0 as i64 * pos.z.0 as i64;
    if d_sq > r_sq && d_sq > 0 {
        // scale = R / sqrt(d_sq) in fixed point
        let d = Fix((d_sq as f64).sqrt() as i32); // only used in non-BPF simcli
        let scale = ARENA_RADIUS.div_fix(d.max(Fix::from_int(1)));
        pos.x = pos.x.mul_fix(scale);
        pos.z = pos.z.mul_fix(scale);
    }
}
