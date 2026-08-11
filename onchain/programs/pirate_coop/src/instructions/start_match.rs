use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState};

#[derive(Accounts)]
pub struct StartMatch<'info> {
    pub authority: Signer<'info>,
    #[account(mut, constraint = match_account.load()?.authority == authority.key())]
    pub match_account: AccountLoader<'info, MatchAccount>,
}

pub fn handler(ctx: Context<StartMatch>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    require!(m.state == MatchState::Lobby as u8, PirateCoopError::NotInLobby);
    require!(m.player_count > 0, PirateCoopError::NotPlayer);
    for i in 0..m.player_count as usize {
        require!(m.players[i].ready == 1, PirateCoopError::NotInLobby);
    }
    m.state = MatchState::Running as u8;
    m.tick = 0;
    Ok(())
}
