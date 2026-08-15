use anchor_lang::prelude::*;
use crate::delegation::commit_and_undelegate_match;
use crate::state::{MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
pub struct EndMatch<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut)]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    /// CHECK: MagicBlock magic context PDA
    pub magic_context: UncheckedAccount<'info>,
    /// CHECK: MagicBlock magic program
    pub magic_program: UncheckedAccount<'info>,
}

pub fn handler(ctx: Context<EndMatch>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    m.state = MatchState::Ended as u8;

    if m.delegated == 1 {
        commit_and_undelegate_match(
            &ctx.accounts.payer.to_account_info(),
            &ctx.accounts.magic_context.to_account_info(),
            &ctx.accounts.magic_program.to_account_info(),
            &ctx.accounts.match_account.to_account_info(),
            &ctx.accounts.snapshot.to_account_info(),
        )?;
        m.delegated = 0;
    }
    Ok(())
}
