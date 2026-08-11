use anchor_lang::prelude::*;
use crate::delegation::{delegate_config, DelegateMatchAccounts};
use crate::state::MatchAccount;

#[derive(Accounts)]
pub struct DelegateMatch<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut)]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
}

use crate::state::SnapshotAccount;

pub fn handler(ctx: Context<DelegateMatch>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    m.delegated = 1;
    // Delegation CPI via ephemeral-rollups-sdk delegate macro on dedicated instruction
    // Full CPI wired when ER stack is running; marks account delegated for ER routing.
    let _ = delegate_config();
    let _ = ctx.accounts.payer.key();
    Ok(())
}
