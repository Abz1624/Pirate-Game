use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::ship_sim::integrate_ship;
use crate::snapshot_tail::{header_mut, read_player, spawn_player, write_player};
use crate::state::{GameConfigAccount, MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
pub struct TickPhase<'info> {
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    #[account(seeds = [b"config"], bump)]
    pub config: AccountLoader<'info, GameConfigAccount>,
}

#[derive(Default)]
pub struct SimBuffers {
    pub inputs: [(i8, i8); 12],
}

impl SimBuffers {
    pub fn set_input(&mut self, slot: u8, throttle: i8, steer: i8) {
        if (slot as usize) < self.inputs.len() {
            self.inputs[slot as usize] = (throttle, steer);
        }
    }
}

pub fn phase1(ctx: Context<TickPhase>) -> Result<()> {
    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let mut data = snapshot_info.try_borrow_mut_data()?;
    let mut m = ctx.accounts.match_account.load_mut()?;
    let cfg = ctx.accounts.config.load()?;
    let mut buffers = SimBuffers::default();
    run_tick_phases(&mut m, &mut data, &cfg, &mut buffers)
}

pub fn phase2(ctx: Context<TickPhase>) -> Result<()> {
    phase1(ctx)
}

pub fn phase3(ctx: Context<TickPhase>) -> Result<()> {
    phase1(ctx)
}

pub fn phase4(ctx: Context<TickPhase>) -> Result<()> {
    phase1(ctx)
}

/// Runs all four tick phases and advances match tick.
pub fn run_tick_phases(
    m: &mut MatchAccount,
    snapshot_data: &mut [u8],
    config: &GameConfigAccount,
    buffers: &mut SimBuffers,
) -> Result<()> {
    require!(m.state == MatchState::Running as u8, PirateCoopError::NotRunning);
    let tick_hz = config.tick_hz.max(1);

    // Phase 1: integrate ships from inputs and write snapshot tail.
    for slot in 0..m.player_count as usize {
        let (throttle, steer) = buffers.inputs[slot];
        let mut player = read_player(snapshot_data, slot).unwrap_or_else(|| spawn_player(slot));
        if player.hp_pct == 0 {
            player = spawn_player(slot);
        } else if player.slot != slot as u8 {
            player.slot = slot as u8;
        }

        let sim = &mut m.ship_sim[slot];
        let (new_x, new_z, new_heading) = integrate_ship(
            player.x,
            player.z,
            player.heading,
            &mut sim.vel_x,
            &mut sim.vel_z,
            throttle,
            steer,
            tick_hz,
        );

        player.x = new_x;
        player.z = new_z;
        player.heading = new_heading;
        player.slot = slot as u8;
        if player.hp_pct == 0 {
            player.hp_pct = 255;
        }
        write_player(snapshot_data, slot, &player);
    }

    // Phase 2-4: enemy/weapons/resolve delegated to simcli on host.
    m.tick = m.tick.saturating_add(1);
    if let Some(s) = header_mut(snapshot_data) {
        s.tick = m.tick;
        s.player_count = m.player_count;
    }

    Ok(())
}
