//! Pure fixed-point combat simulation — no Solana dependencies.
//! Shared by the Anchor program and the headless `simcli` runner.

pub mod config;
pub mod enemies;
pub mod executors;
pub mod fix;
pub mod grid;
pub mod pools;
pub mod prng;
pub mod snapshot;
pub mod status;
pub mod tick;
pub mod trig;
pub mod world;

pub use config::*;
pub use fix::Fix;
pub use tick::{TickPhase, TickResult};
pub use world::MatchWorld;

/// Default simulation tick rate (Hz).
pub const DEFAULT_TICK_HZ: u8 = 10;
/// Maximum players per lobby.
pub const MAX_PLAYERS: usize = 12;
/// Maximum enemies (Tide 8 worst case).
pub const MAX_ENEMIES: usize = 256;
/// Maximum concurrent projectiles.
pub const MAX_PROJECTILES: usize = 1024;
/// Maximum area effects (trails, telegraphs, AOE).
pub const MAX_AREAS: usize = 64;
/// Spatial grid dimension (arena ~200 units).
pub const GRID_CELLS: usize = 32;
/// Arena radius in fixed-point units.
pub const ARENA_RADIUS: Fix = Fix(200 << 16);
