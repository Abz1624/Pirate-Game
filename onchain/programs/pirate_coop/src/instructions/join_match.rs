use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{MatchAccount, MatchState, PlayerSlot};

#[derive(Accounts)]
pub struct JoinMatch<'info> {
    pub owner: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
}

pub fn handler(ctx: Context<JoinMatch>, session_key: Pubkey) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    require!(m.state == MatchState::Lobby as u8, PirateCoopError::NotInLobby);
    require!(m.player_count < m.capacity, PirateCoopError::MatchFull);

    for slot in m.players.iter_mut() {
        if slot.owner == Pubkey::default() {
            slot.owner = ctx.accounts.owner.key();
            slot.session_key = session_key;
            slot.ready = 0;
            m.player_count += 1;
            return Ok(());
        }
    }
    err!(PirateCoopError::MatchFull)
}
