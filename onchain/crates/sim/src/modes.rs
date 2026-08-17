//! Match mode and PvPvE constants.

/// Players in a PvPvE arena match.
pub const PVP_MAX_PLAYERS: usize = 4;
/// Enemy cap for PvPvE (fits CU budget).
pub const PVP_MAX_ENEMIES: usize = 96;
pub const PVP_MAX_PROJECTILES: usize = 64;
pub const PVP_MAX_AREAS: usize = 16;
pub const PVP_MAX_ORBS: usize = 32;

/// Match duration: 8 minutes at 10 Hz.
pub const MATCH_DURATION_TICKS: u32 = 8 * 60 * 10;
/// Kraken spawns at 5:00.
pub const KRAKEN_SPAWN_TICK: u32 = 5 * 60 * 10;
/// Storm starts at 6:00.
pub const STORM_START_TICK: u32 = 6 * 60 * 10;
/// Storm fully contracted by 7:30.
pub const STORM_END_TICK: u32 = 7 * 60 * 10 + 30 * 10;

/// Respawn delay in ticks (8s @ 10Hz).
pub const RESPAWN_TICKS: u16 = 80;
/// Spawn protection after respawn (3s @ 10Hz).
pub const SPAWN_PROTECTION_TICKS: u16 = 30;
/// Plunder orb TTL (20s @ 10Hz).
pub const ORB_TTL_TICKS: u16 = 200;

/// Plunder drop fraction of bank (25%).
pub const PLUNDER_DROP_PCT: u8 = 25;
/// Fraction of drop that goes instantly to killer (50%).
pub const PLUNDER_KILLER_PCT: u8 = 50;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum MatchMode {
    #[default]
    Coop = 0,
    PvPvE = 1,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum KrakenState {
    #[default]
    None = 0,
    Spawned = 1,
    Dead = 2,
}
