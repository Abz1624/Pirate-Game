use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::instructions::tick::{run_tick_phases, SimBuffers};
use crate::state::{MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
pub struct SubmitInput<'info> {
    pub player: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    #[account(seeds = [b"config"], bump)]
    pub config: AccountLoader<'info, crate::state::GameConfigAccount>,
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
    let _ = buttons;

    let mut buffers = SimBuffers::default();
    buffers.set_input(slot_idx.unwrap() as u8, throttle, steer);
    let cfg = ctx.accounts.config.load()?;

    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let mut data = snapshot_info.try_borrow_mut_data()?;
    run_tick_phases(&mut m, &mut data, &cfg, &mut buffers)?;
    Ok(())
}
