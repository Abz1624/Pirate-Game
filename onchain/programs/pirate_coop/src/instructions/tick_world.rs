use anchor_lang::prelude::*;
use anchor_lang::solana_program::sysvar::clock::Clock;
use pirate_sim::{world_from_bytes_mut, Fix, ShipInput};
use crate::errors::PirateCoopError;
use crate::snapshot_tail::write_snapshot_from_sim;
use crate::state::{MatchAccount, MatchState, SnapshotAccount, MAX_PLAYERS};

#[derive(Accounts)]
pub struct TickWorld<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    /// CHECK: world PDA bytes
    #[account(
        mut,
        seeds = [b"world", match_account.key().as_ref()],
        bump,
    )]
    pub world: UncheckedAccount<'info>,
    #[account(seeds = [b"config"], bump)]
    pub config: AccountLoader<'info, crate::state::GameConfigAccount>,
}

pub fn handler(
    ctx: Context<TickWorld>,
    max_ticks: u8,
    input_count: u8,
    throttles: [i8; MAX_PLAYERS],
    steers: [i8; MAX_PLAYERS],
) -> Result<()> {
    let tick_hz = ctx.accounts.config.load()?.tick_hz.max(1);
    let clock = Clock::get()?;
    let slot = clock.slot;
    let slots_per_tick = 1u64;
    let max_ticks = max_ticks.max(1);

    let (player_count, tick) = {
        let m = ctx.accounts.match_account.load()?;
        require!(m.state == MatchState::Running as u8, PirateCoopError::NotRunning);
        let player_count = m.player_count;

        let mut world_data = ctx.accounts.world.try_borrow_mut_data()?;
        let world_bytes = &mut world_data[8..8 + pirate_sim::WORLD_STATE_LEN];
        let world = world_from_bytes_mut(world_bytes).ok_or(PirateCoopError::InvalidCapacity)?;

        if input_count > 0 {
            let apply_count = input_count.min(player_count).min(MAX_PLAYERS as u8) as usize;
            for slot_idx in 0..apply_count {
                world.pending_inputs[slot_idx] = ShipInput {
                    throttle: throttles[slot_idx],
                    steer: steers[slot_idx],
                    buttons: 0,
                };
            }
        }

        if world.header.last_crank_slot > 0 && slot < world.header.last_crank_slot + slots_per_tick {
            return Ok(());
        }

        let dt = Fix::from_int(1) / Fix::from_int(tick_hz as i32);
        for _ in 0..max_ticks {
            for player_slot in 0..player_count as usize {
                let input = world.pending_inputs[player_slot];
                let player = &mut world.players[player_slot];
                if !player.active {
                    continue;
                }
                pirate_sim::tick::integrate_ship(player, input, dt);
                pirate_sim::world::clamp_position_to_arena(&mut player.position);
            }
            world.header.tick = world.header.tick.saturating_add(1);
            world.header.match_timer = world.header.match_timer.saturating_add(1);
        }
        world.header.last_crank_slot = slot;
        (player_count, world.header.tick)
    };

    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let mut snap_data = snapshot_info.try_borrow_mut_data()?;
    let world_data = ctx.accounts.world.try_borrow_data()?;
    if let Some(world) = pirate_sim::world_from_bytes(&world_data[8..8 + pirate_sim::WORLD_STATE_LEN]) {
        let mut snap = pirate_sim::snapshot::Snapshot::default();
        snap.tick = tick;
        write_snapshot_from_sim(&mut snap_data, &snap, world, player_count);
    }

    Ok(())
}
