use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState};

#[derive(Accounts)]
pub struct DraftPick<'info> {
    pub player: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
}

/// Draft pick uses VRF seed from client (verified off-chain / via MagicBlock VRF oracle).
pub fn handler(ctx: Context<DraftPick>, card_index: u8, vrf_seed: [u8; 32]) -> Result<()> {
    let m = ctx.accounts.match_account.load()?;
    require!(m.state == MatchState::Running as u8, PirateCoopError::NotRunning);
    let _ = (card_index, vrf_seed, ctx.accounts.player.key());
    // Offer generation: 3 cards, evolution partners 3x weight, maxed excluded (docs/03).
    Ok(())
}
