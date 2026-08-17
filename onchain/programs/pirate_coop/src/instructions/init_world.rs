use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{world_space, MatchAccount};

/// Solana max account data growth per instruction (delegation CPI limit).
const MAX_ACCOUNT_GROWTH: usize = 10_240;

#[derive(Accounts)]
pub struct InitWorld<'info> {
    #[account(mut)]
    pub authority: Signer<'info>,
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(
        init,
        payer = authority,
        space = world_space(),
        seeds = [b"world", match_account.key().as_ref()],
        bump
    )]
    /// CHECK: raw world state bytes (pirate_sim::WorldState)
    pub world: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

pub fn handler(ctx: Context<InitWorld>) -> Result<()> {
    require!(
        world_space() <= MAX_ACCOUNT_GROWTH,
        PirateCoopError::InvalidCapacity
    );
    let m = ctx.accounts.match_account.load()?;
    require!(
        m.authority == ctx.accounts.authority.key(),
        PirateCoopError::InvalidCapacity
    );
    Ok(())
}
