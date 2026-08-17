//! Match world state.

use crate::config::{Element, GameConfig};
use crate::fix::Fix;
use crate::fixed_pool::FixedPool;
use crate::grid::SpatialGrid;
use crate::modes::{
    MatchMode, PVP_MAX_AREAS, PVP_MAX_ENEMIES, PVP_MAX_ORBS, PVP_MAX_PLAYERS, PVP_MAX_PROJECTILES,
};
use crate::pools::{EntityId, EntityKind, Vec2};
use crate::prng::Prng;
use crate::snapshot::Snapshot;
use crate::status::StatusState;
use crate::world_state::{OrbState, WorldState};
use crate::ARENA_RADIUS;

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
    pub bank: u32,
    pub respawn_timer: u16,
    pub player_kills: u16,
    pub level_ups_pending: u8,
    pub draft_offers: [u8; 3],
    pub placement: u8,
    pub pending_throttle: i8,
    pub pending_steer: i8,
    pub spawn_protection: u16,
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
            level: 1,
            gold: 0,
            kills: 0,
            bank: 0,
            damage_dealt: 0,
            revives: 0,
            respawn_timer: 0,
            player_kills: 0,
            level_ups_pending: 0,
            draft_offers: [0; 3],
            placement: 0,
            pending_throttle: 0,
            pending_steer: 0,
            spawn_protection: 0,
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
            // Degrees per second. Heading itself is encoded as 1024 units/revolution.
            turn_rate: Fix::from_int(800),
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
    Boss = 6,
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
    pub is_boss: bool,
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
    pub hits_players: bool,
}

pub struct MatchWorld {
    pub inner: WorldState,
    pub grid: SpatialGrid,
    pub snapshot: Snapshot,
}

impl MatchWorld {
    pub fn new(seed: u64, tide: u8, capacity: u8, config: &GameConfig) -> Self {
        let cap = capacity.min(PVP_MAX_PLAYERS as u8);
        let max_enemies = config
            .tides
            .get(tide.saturating_sub(1) as usize)
            .map(|t| t.max_enemies as usize)
            .unwrap_or(60)
            .min(PVP_MAX_ENEMIES);
        Self {
            inner: WorldState::new(seed, tide, cap, MatchMode::PvPvE),
            grid: SpatialGrid::default(),
            snapshot: Snapshot::with_capacity(cap as usize, max_enemies),
        }
    }

    pub fn phase(&self) -> MatchPhase {
        self.inner.phase()
    }

    pub fn set_phase(&mut self, phase: MatchPhase) {
        self.inner.set_phase(phase);
    }

    pub fn tick(&self) -> u32 {
        self.inner.header.tick
    }

    pub fn set_tick(&mut self, tick: u32) {
        self.inner.header.tick = tick;
    }

    pub fn header(&self) -> &crate::world_state::WorldHeader {
        &self.inner.header
    }

    pub fn header_mut(&mut self) -> &mut crate::world_state::WorldHeader {
        &mut self.inner.header
    }

    pub fn players(&self) -> &[PlayerState; PVP_MAX_PLAYERS] {
        &self.inner.players
    }

    pub fn players_mut(&mut self) -> &mut [PlayerState; PVP_MAX_PLAYERS] {
        &mut self.inner.players
    }

    pub fn enemies(&self) -> &FixedPool<EnemyState, PVP_MAX_ENEMIES> {
        &self.inner.enemies
    }

    pub fn enemies_mut(&mut self) -> &mut FixedPool<EnemyState, PVP_MAX_ENEMIES> {
        &mut self.inner.enemies
    }

    pub fn projectiles(&self) -> &FixedPool<ProjectileState, PVP_MAX_PROJECTILES> {
        &self.inner.projectiles
    }

    pub fn projectiles_mut(&mut self) -> &mut FixedPool<ProjectileState, PVP_MAX_PROJECTILES> {
        &mut self.inner.projectiles
    }

    pub fn areas(&self) -> &FixedPool<AreaState, PVP_MAX_AREAS> {
        &self.inner.areas
    }

    pub fn areas_mut(&mut self) -> &mut FixedPool<AreaState, PVP_MAX_AREAS> {
        &mut self.inner.areas
    }

    pub fn orbs(&self) -> &FixedPool<OrbState, PVP_MAX_ORBS> {
        &self.inner.orbs
    }

    pub fn orbs_mut(&mut self) -> &mut FixedPool<OrbState, PVP_MAX_ORBS> {
        &mut self.inner.orbs
    }

    pub fn player_count(&self) -> u8 {
        self.inner.header.player_count
    }

    pub fn seed(&self) -> u64 {
        self.inner.header.seed
    }

    pub fn mode(&self) -> MatchMode {
        self.inner.mode()
    }

    pub fn prng(&self) -> Prng {
        self.inner.prng()
    }

    pub fn set_prng(&mut self, prng: Prng) {
        self.inner.set_prng(prng);
    }

    pub fn join_player(&mut self, slot: u8) -> bool {
        self.inner.join_player(slot)
    }

    pub fn state_hash(&self) -> u64 {
        self.inner.state_hash()
    }

    pub fn clamp_to_arena(&self, pos: &mut Vec2) {
        crate::world_state::clamp_position_to_arena_int(pos);
    }
}

pub fn clamp_position_to_arena(pos: &mut Vec2) {
    crate::world_state::clamp_position_to_arena_int(pos);
}
