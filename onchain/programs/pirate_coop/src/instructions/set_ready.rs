use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState};

#[derive(Accounts)]
pub struct SetReady<'info> {
    pub player: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
}

pub fn handler(ctx: Context<SetReady>, ready: bool) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    require!(m.state == MatchState::Lobby as u8, PirateCoopError::NotInLobby);
    let key = ctx.accounts.player.key();
    let mut found = false;
    for slot in m.players.iter_mut() {
        if slot.owner == key {
            slot.ready = if ready { 1 } else { 0 };
            found = true;
            break;
        }
    }
    require!(found, PirateCoopError::NotPlayer);
    Ok(())
}
