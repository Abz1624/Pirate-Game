use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::snapshot_tail::{header_mut, spawn_player, write_player};
use crate::state::{MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
pub struct StartMatch<'info> {
    pub authority: Signer<'info>,
    #[account(mut, constraint = match_account.load()?.authority == authority.key())]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
}

pub fn handler(ctx: Context<StartMatch>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    require!(m.state == MatchState::Lobby as u8, PirateCoopError::NotInLobby);
    require!(m.player_count > 0, PirateCoopError::NotPlayer);
    for i in 0..m.player_count as usize {
        require!(m.players[i].ready == 1, PirateCoopError::NotInLobby);
    }

    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let mut data = snapshot_info.try_borrow_mut_data()?;

    for slot in 0..m.player_count as usize {
        m.ship_sim[slot] = Default::default();
        let spawn_x = ((slot as i16) * 4 - 6).clamp(-20, 20);
        let mut player = spawn_player(slot);
        player.x = spawn_x;
        write_player(&mut data, slot, &player);
    }

    m.state = MatchState::Running as u8;
    m.tick = 0;

    if let Some(s) = header_mut(&mut data) {
        s.tick = 0;
        s.player_count = m.player_count;
        s.enemy_count = 0;
        s.projectile_count = 0;
    }

    Ok(())
}
