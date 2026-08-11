use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::GameConfigAccount;

#[derive(Accounts)]
pub struct UpdateConfig<'info> {
    pub authority: Signer<'info>,
    #[account(mut, seeds = [b"config"], bump, has_one = authority)]
    pub config: Account<'info, GameConfigAccount>,
}

pub fn handler(ctx: Context<UpdateConfig>, data: Vec<u8>) -> Result<()> {
    require!(
        data.len() <= crate::state::MAX_CONFIG_BYTES,
        PirateCoopError::ConfigTooLarge
    );
    let cfg = &mut ctx.accounts.config;
    cfg.data[..data.len()].copy_from_slice(&data);
    cfg.data_len = data.len() as u16;
    Ok(())
}
