use anchor_lang::prelude::*;
use crate::state::GameConfigAccount;

#[derive(Accounts)]
pub struct InitializeConfig<'info> {
    #[account(mut)]
    pub authority: Signer<'info>,
    #[account(
        init,
        payer = authority,
        space = 8 + GameConfigAccount::INIT_SPACE,
        seeds = [b"config"],
        bump
    )]
    pub config: Account<'info, GameConfigAccount>,
    pub system_program: Program<'info, System>,
}

pub fn handler(ctx: Context<InitializeConfig>, version: u32) -> Result<()> {
    let cfg = &mut ctx.accounts.config;
    cfg.authority = ctx.accounts.authority.key();
    cfg.version = version;
    cfg.lobby_capacity_default = 4;
    cfg.tick_hz = 10;
    cfg.convoy_buff_cap_pct = 24;
    cfg.data_len = 0;
    Ok(())
}
