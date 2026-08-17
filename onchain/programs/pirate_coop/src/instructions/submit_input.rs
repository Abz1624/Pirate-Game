use anchor_lang::prelude::*;
use pirate_sim::{world_from_bytes_mut, ShipInput};
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState};

#[derive(Accounts)]
pub struct SubmitInput<'info> {
    pub player: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    /// CHECK: world PDA
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
}

pub fn handler(ctx: Context<SubmitInput>, throttle: i8, steer: i8, buttons: u8) -> Result<()> {
    let m = ctx.accounts.match_account.load()?;
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

    let mut world_data = ctx.accounts.world.try_borrow_mut_data()?;
    let world_bytes = &mut world_data[8..8 + pirate_sim::WORLD_STATE_LEN];
    let world = world_from_bytes_mut(world_bytes).ok_or(PirateCoopError::InvalidCapacity)?;
    world.set_input(
        slot_idx.unwrap() as u8,
        ShipInput {
            throttle,
            steer,
            buttons,
        },
    );
    Ok(())
}
