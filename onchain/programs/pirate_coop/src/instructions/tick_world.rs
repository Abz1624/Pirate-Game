use anchor_lang::prelude::*;
use anchor_lang::solana_program::sysvar::clock::Clock;
use pirate_sim::config::GameConfig;
use pirate_sim::{run_world_tick, world_from_bytes_mut};
use crate::errors::PirateCoopError;
use crate::snapshot_tail::write_snapshot_from_sim;
use crate::state::{MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
pub struct TickWorld<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    /// CHECK: world PDA bytes
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
    #[account(seeds = [b"config"], bump)]
    pub config: AccountLoader<'info, crate::state::GameConfigAccount>,
}

pub fn handler(ctx: Context<TickWorld>, max_ticks: u8) -> Result<()> {
    let _tick_hz = ctx.accounts.config.load()?.tick_hz.max(1);
    let clock = Clock::get()?;
    let slot = clock.slot;
    let slots_per_tick = 1u64;
    let max_ticks = max_ticks.max(1) as u32;

    let (snap, player_count, ended) = {
        let mut m = ctx.accounts.match_account.load_mut()?;
        require!(m.state == MatchState::Running as u8, PirateCoopError::NotRunning);

        let mut world_data = ctx.accounts.world.try_borrow_mut_data()?;
        let world_bytes = &mut world_data[8..8 + pirate_sim::WORLD_STATE_LEN];
        let world = world_from_bytes_mut(world_bytes).ok_or(PirateCoopError::InvalidCapacity)?;

        if world.header.last_crank_slot > 0 && slot < world.header.last_crank_slot + slots_per_tick {
            return Ok(());
        }

        let game_config = GameConfig::dev_defaults();
        let mut snap = pirate_sim::snapshot::Snapshot::default();
        let mut ended = false;
        for _ in 0..max_ticks {
            let (result, tick_snap) = run_world_tick(world, &game_config);
            let _ = result;
            snap = tick_snap;
            m.tick = world.header.tick;
            ended = world.header.phase == MatchState::Ended as u8;
            if ended {
                m.state = MatchState::Ended as u8;
                break;
            }
        }
        world.header.last_crank_slot = slot;
        (snap, m.player_count, ended)
    };

    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let mut snap_data = snapshot_info.try_borrow_mut_data()?;
    let world_data = ctx.accounts.world.try_borrow_data()?;
    if let Some(world) = pirate_sim::world_from_bytes(&world_data[8..8 + pirate_sim::WORLD_STATE_LEN]) {
        write_snapshot_from_sim(&mut snap_data, &snap, world, player_count);
    }

    let _ = ended;
    Ok(())
}
