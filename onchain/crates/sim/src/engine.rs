//! High-level tick entry for on-chain and headless runners.

use crate::config::GameConfig;
use crate::snapshot::Snapshot;
use crate::tick::{run_full_tick, TickResult};
use crate::world::{MatchPhase, MatchWorld};
use crate::world_state::WorldState;
use crate::WORLD_STATE_LEN;

#[cfg(feature = "bpf")]
use std::boxed::Box;

/// Run one authoritative tick from account-backed world state.
pub fn run_world_tick(state: &mut WorldState, config: &GameConfig) -> (TickResult, Snapshot) {
    let cap = state.header.capacity.max(1);
    let max_enemies = 96usize;
    #[cfg(feature = "bpf")]
    {
        let mut world = Box::new(MatchWorld {
            inner: core::mem::take(state),
            grid: crate::grid::SpatialGrid::default(),
            snapshot: Snapshot::with_capacity(cap as usize, max_enemies),
        });
        if world.phase() == MatchPhase::Lobby {
            world.set_phase(MatchPhase::Running);
        }
        let result = run_full_tick(&mut world, config);
        let snap = world.snapshot.clone();
        *state = core::mem::take(&mut world.inner);
        return (result, snap);
    }
    #[cfg(not(feature = "bpf"))]
    {
        let mut world = MatchWorld {
            inner: core::mem::take(state),
            grid: crate::grid::SpatialGrid::default(),
            snapshot: Snapshot::with_capacity(cap as usize, max_enemies),
        };
        if world.phase() == MatchPhase::Lobby {
            world.set_phase(MatchPhase::Running);
        }
        let result = run_full_tick(&mut world, config);
        let snap = world.snapshot.clone();
        *state = world.inner;
        (result, snap)
    }
}

/// Apply draft pick directly on account-backed world state.
pub fn apply_draft_pick_world(state: &mut WorldState, slot: u8, card_index: u8) -> bool {
    #[cfg(feature = "bpf")]
    {
        let mut mw = Box::new(MatchWorld {
            inner: core::mem::take(state),
            grid: crate::grid::SpatialGrid::default(),
            snapshot: Snapshot::default(),
        });
        let ok = crate::gameplay::apply_draft_pick(&mut mw, slot, card_index);
        *state = core::mem::take(&mut mw.inner);
        return ok;
    }
    #[cfg(not(feature = "bpf"))]
    {
        let mut mw = MatchWorld::new(
            state.header.seed,
            state.header.tide,
            state.header.capacity,
            &GameConfig::dev_defaults(),
        );
        mw.inner = core::mem::take(state);
        let ok = crate::gameplay::apply_draft_pick(&mut mw, slot, card_index);
        *state = mw.inner;
        ok
    }
}

/// Initialize world state when a match starts on-chain.
pub fn init_world_for_match(state: &mut WorldState, player_count: u8, mode: u8) {
    state.header.player_count = player_count;
    state.header.mode = mode;
    state.init_match_start();
}

/// Load world state from account bytes (must be exact size).
pub fn world_from_bytes_mut(data: &mut [u8]) -> Option<&mut WorldState> {
    if data.len() < WORLD_STATE_LEN {
        return None;
    }
    Some(unsafe { &mut *(data.as_mut_ptr() as *mut WorldState) })
}

/// Load world state from account bytes.
pub fn world_from_bytes(data: &[u8]) -> Option<&WorldState> {
    if data.len() < WORLD_STATE_LEN {
        return None;
    }
    Some(unsafe { &*(data.as_ptr() as *const WorldState) })
}
