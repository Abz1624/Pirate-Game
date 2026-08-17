use anchor_lang::prelude::*;
use anchor_lang::system_program;
use crate::errors::PirateCoopError;
use crate::state::{world_space, MatchAccount};

const MAX_ACCOUNT_GROWTH: usize = 10_240;

#[derive(Accounts)]
pub struct ExtendWorld<'info> {
    #[account(mut)]
    pub authority: Signer<'info>,
    #[account(
        constraint = match_account.load()?.authority == authority.key() @ PirateCoopError::InvalidCapacity,
    )]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(
        mut,
        seeds = [b"world", match_account.key().as_ref()],
        bump,
    )]
    /// CHECK: raw world state bytes
    pub world: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

pub fn handler(ctx: Context<ExtendWorld>) -> Result<()> {
    let target = world_space();
    let world = ctx.accounts.world.to_account_info();
    let current = world.data_len();
    if current >= target {
        return Ok(());
    }

    let grow = (target - current).min(MAX_ACCOUNT_GROWTH);
    let new_len = current.saturating_add(grow);

    let rent = Rent::get()?;
    let required = rent.minimum_balance(new_len);
    let top_up = required.saturating_sub(world.lamports());
    if top_up > 0 {
        system_program::transfer(
            CpiContext::new(
                ctx.accounts.system_program.to_account_info(),
                system_program::Transfer {
                    from: ctx.accounts.authority.to_account_info(),
                    to: world.clone(),
                },
            ),
            top_up,
        )?;
    }

    world.realloc(new_len, false)?;

    if new_len >= target {
        let mut data = world.try_borrow_mut_data()?;
        require!(
            data.len() >= 8 + pirate_sim::WORLD_STATE_LEN,
            PirateCoopError::InvalidCapacity
        );
        data[8..8 + pirate_sim::WORLD_STATE_LEN].fill(0);
    }

    Ok(())
}
