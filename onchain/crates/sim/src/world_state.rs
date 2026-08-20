//! Account-backed world state — fixed-size, BPF-safe.

use crate::config::Element;
use crate::fix::Fix;
use crate::fixed_pool::{isqrt64, FixedPool};
use crate::modes::{
    KrakenState, MatchMode, PVP_MAX_AREAS, PVP_MAX_ENEMIES, PVP_MAX_ORBS, PVP_MAX_PLAYERS,
    PVP_MAX_PROJECTILES,
};
use crate::pools::Vec2;
use crate::prng::Prng;
use crate::status::StatusState;
use crate::world::{
    AreaState, EnemyState, MatchPhase, PassiveSlot, PlayerState, ProjectileState, ShipInput,
    WeaponSlot,
};
use crate::ARENA_RADIUS;

/// Deterministic spawn X/Z in world units (quantized integers).
pub fn spawn_xy_for_slot(slot: usize, player_count: u8) -> (i32, i32) {
    let n = player_count.max(1) as usize;
    if n == 2 {
        return if slot == 0 { (35, 0) } else { (-35, 0) };
    }
    if n <= 4 {
        const CORNERS: [(i32, i32); 4] = [(35, 0), (-35, 0), (0, 35), (0, -35)];
        return CORNERS[slot % 4];
    }
    let angle = (slot as u32 * 256 / n as u32) as u16;
    let (sx, sz) = crate::trig::sin_cos(angle);
    let dist = Fix::from_int(35);
    (
        crate::snapshot::quantize_pos(sx.mul_fix(dist)) as i32,
        crate::snapshot::quantize_pos(sz.mul_fix(dist)) as i32,
    )
}

/// Serialized world header.
#[derive(Clone, Copy, Debug, Default)]
#[repr(C)]
pub struct WorldHeader {
    pub tick: u32,
    pub seed: u64,
    pub tide: u8,
    pub capacity: u8,
    pub player_count: u8,
    pub phase: u8,
    pub mode: u8,
    pub kraken_state: u8,
    pub storm_radius_pct: u8, // 0-100 of arena
    pub match_timer: u32,
    pub prng: [u32; 4],
    pub spawn_accumulator_raw: i32,
    pub wave_index: u16,
    pub last_crank_slot: u64,
    pub kraken_entity: u16,
    pub _pad: [u8; 3],
}

#[derive(Clone, Copy, Debug, Default)]
#[repr(C)]
pub struct OrbState {
    pub x: i16,
    pub z: i16,
    pub value: u32,
    pub ttl: u16,
    pub owner_slot: u8,
    pub _pad: u8,
}

/// Full simulation state stored in a delegated account.
#[derive(Clone, Debug)]
#[repr(C)]
pub struct WorldState {
    pub header: WorldHeader,
    pub players: [PlayerState; PVP_MAX_PLAYERS],
    pub pending_inputs: [ShipInput; PVP_MAX_PLAYERS],
    pub enemies: FixedPool<EnemyState, PVP_MAX_ENEMIES>,
    pub projectiles: FixedPool<ProjectileState, PVP_MAX_PROJECTILES>,
    pub areas: FixedPool<AreaState, PVP_MAX_AREAS>,
    pub orbs: FixedPool<OrbState, PVP_MAX_ORBS>,
}

impl Default for WorldState {
    fn default() -> Self {
        Self::new(0, 1, 4, MatchMode::PvPvE)
    }
}

impl WorldState {
    pub fn new(seed: u64, tide: u8, capacity: u8, mode: MatchMode) -> Self {
        let cap = capacity.min(PVP_MAX_PLAYERS as u8);
        Self {
            header: WorldHeader {
                seed,
                tide,
                capacity: cap,
                phase: MatchPhase::Lobby as u8,
                mode: mode as u8,
                storm_radius_pct: 100,
                prng: Prng::new(seed).state(),
                ..Default::default()
            },
            players: std::array::from_fn(|_| PlayerState::default()),
            pending_inputs: [ShipInput::default(); PVP_MAX_PLAYERS],
            enemies: FixedPool::new(),
            projectiles: FixedPool::new(),
            areas: FixedPool::new(),
            orbs: FixedPool::new(),
        }
    }

    pub fn prng(&self) -> Prng {
        Prng::from_state(self.header.prng)
    }

    pub fn set_prng(&mut self, prng: Prng) {
        self.header.prng = prng.state();
    }

    pub fn phase(&self) -> MatchPhase {
        match self.header.phase {
            1 => MatchPhase::Running,
            2 => MatchPhase::Ended,
            _ => MatchPhase::Lobby,
        }
    }

    pub fn set_phase(&mut self, phase: MatchPhase) {
        self.header.phase = phase as u8;
    }

    pub fn mode(&self) -> MatchMode {
        if self.header.mode == MatchMode::PvPvE as u8 {
            MatchMode::PvPvE
        } else {
            MatchMode::Coop
        }
    }

    pub fn spawn_accumulator(&self) -> Fix {
        Fix(self.header.spawn_accumulator_raw)
    }

    pub fn set_spawn_accumulator(&mut self, v: Fix) {
        self.header.spawn_accumulator_raw = v.0;
    }

    pub fn join_player(&mut self, slot: u8) -> bool {
        let s = slot as usize;
        if s >= PVP_MAX_PLAYERS || self.players[s].active {
            return false;
        }
        let mut prng = self.prng();
        self.players[s].active = true;
        self.players[s].position = Vec2::new(
            Fix::from_int(prng.range_i32(-20, 20)),
            Fix::from_int(prng.range_i32(-20, 20)),
        );
        self.players[s].hp = self.players[s].max_hp;
        self.players[s].bank = 0;
        self.players[s].xp = 0;
        self.players[s].level = 1;
        self.header.player_count = self.header.player_count.saturating_add(1);
        self.set_prng(prng);
        true
    }

    pub fn set_input(&mut self, slot: u8, input: ShipInput) {
        if (slot as usize) < PVP_MAX_PLAYERS {
            self.pending_inputs[slot as usize] = input;
            self.players[slot as usize].pending_throttle = input.throttle;
            self.players[slot as usize].pending_steer = input.steer;
        }
    }

    pub fn init_match_start(&mut self) {
        self.header.tick = 0;
        self.header.match_timer = 0;
        self.header.storm_radius_pct = 100;
        self.header.kraken_state = KrakenState::None as u8;
        self.header.kraken_entity = u16::MAX;
        self.set_phase(MatchPhase::Running);
        let mut prng = self.prng();
        for slot in 0..self.header.player_count as usize {
            // Use integer spawn offsets — the 256-entry sin table underestimates cos(0)
            // and produced positions like (0,8) instead of (35,0).
            let (px, pz) = spawn_xy_for_slot(slot, self.header.player_count);
            self.players[slot].position = Vec2::new(Fix::from_int(px), Fix::from_int(pz));
            self.players[slot].velocity = Vec2::default();
            self.players[slot].heading =
                (slot as u32 * 256 / self.header.player_count.max(1) as u32) as u16;
            self.players[slot].hp = self.players[slot].max_hp;
            self.players[slot].active = true;
            self.players[slot].bank = 0;
            self.players[slot].level = 1;
            self.players[slot].weapon_slots = [WeaponSlot {
                weapon_id: 1,
                level: 1,
            }; 6];
            self.players[slot].passive_slots = [PassiveSlot::default(); 6];
        }
        self.set_prng(prng);
    }

    pub fn state_hash(&self) -> u64 {
        let mut h = self.header.tick as u64 ^ self.header.seed;
        for (i, p) in self.players.iter().enumerate() {
            if p.active {
                h ^= (i as u64 + 1).wrapping_mul(p.position.x.0 as u64);
                h ^= p.hp as u64;
                h ^= p.bank as u64;
            }
        }
        h
    }
}

/// Serialized byte length of account-backed world state.
pub const WORLD_STATE_LEN: usize = core::mem::size_of::<WorldState>();

/// Integer arena clamp (no f64).
pub fn clamp_position_to_arena_int(pos: &mut Vec2) {
    let r_sq = (ARENA_RADIUS.0 as i64) * (ARENA_RADIUS.0 as i64);
    let d_sq = pos.x.0 as i64 * pos.x.0 as i64 + pos.z.0 as i64 * pos.z.0 as i64;
    if d_sq > r_sq && d_sq > 0 {
        let d = isqrt64(d_sq).max(1);
        let scale = ((ARENA_RADIUS.0 as i64) << 16) / d;
        pos.x = Fix(((pos.x.0 as i64 * scale) >> 16) as i32);
        pos.z = Fix(((pos.z.0 as i64 * scale) >> 16) as i32);
    }
}
