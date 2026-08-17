use anchor_lang::prelude::*;
use pirate_sim::{apply_draft_pick_world, world_from_bytes_mut};
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState};

#[derive(Accounts)]
pub struct DraftPick<'info> {
    pub player: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    /// CHECK: world PDA
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
}

pub fn handler(ctx: Context<DraftPick>, card_index: u8, vrf_seed: [u8; 32]) -> Result<()> {
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
    let _ = vrf_seed;

    let mut world_data = ctx.accounts.world.try_borrow_mut_data()?;
    let world_bytes = &mut world_data[8..8 + pirate_sim::WORLD_STATE_LEN];
    let world = world_from_bytes_mut(world_bytes).ok_or(PirateCoopError::InvalidCapacity)?;
    require!(
        apply_draft_pick_world(world, slot_idx.unwrap() as u8, card_index),
        PirateCoopError::NotRunning
    );
    Ok(())
}
