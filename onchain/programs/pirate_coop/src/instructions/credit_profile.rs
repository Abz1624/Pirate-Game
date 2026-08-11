use anchor_lang::prelude::*;
use crate::state::{MatchAccount, PlayerProfile};

#[derive(Accounts)]
pub struct CreditProfile<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(
        init_if_needed,
        payer = payer,
        space = 8 + PlayerProfile::INIT_SPACE,
        seeds = [b"profile", payer.key().as_ref()],
        bump
    )]
    pub profile: Account<'info, PlayerProfile>,
    pub system_program: Program<'info, System>,
}

pub fn handler(ctx: Context<CreditProfile>) -> Result<()> {
    let m = ctx.accounts.match_account.load()?;
    let profile = &mut ctx.accounts.profile;
    profile.owner = ctx.accounts.payer.key();
    for slot in m.players.iter() {
        if slot.owner == ctx.accounts.payer.key() {
            profile.gold = profile.gold.saturating_add(slot.gold_earned as u64);
            profile.contribution_score = profile
                .contribution_score
                .saturating_add(slot.contribution as u64);
            profile.runs_completed = profile.runs_completed.saturating_add(1);
            break;
        }
    }
    Ok(())
}
