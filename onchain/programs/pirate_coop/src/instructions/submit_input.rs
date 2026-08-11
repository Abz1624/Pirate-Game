use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState, SnapshotAccount};
use crate::instructions::tick::{run_tick_phases, SimBuffers};

#[derive(Accounts)]
pub struct SubmitInput<'info> {
    pub player: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    #[account(seeds = [b"config"], bump)]
    pub config: Account<'info, crate::state::GameConfigAccount>,
}

pub fn handler(ctx: Context<SubmitInput>, throttle: i8, steer: i8, buttons: u8) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    require!(m.state == MatchState::Running as u8, PirateCoopError::NotRunning);
    let key = ctx.accounts.player.key();
    let mut slot_idx = None;
    for (i, slot) in m.players.iter().enumerate() {
        if slot.owner == key || slot.session_key == key {
            slot_idx = Some(i);
            break;
        }
    }
    require!(slot_idx.is_some(), PirateCoopError::NotPlayer);
    let _ = (throttle, steer, buttons, slot_idx);

    // Input-driven tick advance
    let mut buffers = SimBuffers::default();
    buffers.set_input(slot_idx.unwrap() as u8, throttle, steer);
    run_tick_phases(
        &mut m,
        &mut ctx.accounts.snapshot.load_mut()?,
        &ctx.accounts.config,
        &mut buffers,
    )?;
    Ok(())
}
