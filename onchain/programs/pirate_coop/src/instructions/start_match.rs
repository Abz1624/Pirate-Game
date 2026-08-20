use anchor_lang::prelude::*;
use pirate_sim::{init_world_for_match, world_from_bytes_mut};
use crate::errors::PirateCoopError;
use crate::snapshot_tail::{header_mut, write_snapshot_from_sim};
use crate::state::{MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
pub struct StartMatch<'info> {
    pub authority: Signer<'info>,
    #[account(mut, constraint = match_account.load()?.authority == authority.key())]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, constraint = snapshot.load()?.match_key == match_account.key())]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    /// CHECK: world PDA
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
}

pub fn handler(ctx: Context<StartMatch>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    require!(m.state == MatchState::Lobby as u8, PirateCoopError::NotInLobby);
    require!(m.player_count > 0, PirateCoopError::NotPlayer);
    for i in 0..m.player_count as usize {
        require!(m.players[i].ready == 1, PirateCoopError::NotInLobby);
    }

    let mut world_data = ctx.accounts.world.try_borrow_mut_data()?;
    let world_bytes = &mut world_data[8..8 + pirate_sim::WORLD_STATE_LEN];
    let world = world_from_bytes_mut(world_bytes).ok_or(PirateCoopError::InvalidCapacity)?;
    init_world_for_match(world, m.player_count, m.mode);

    m.state = MatchState::Running as u8;
    m.tick = 0;

    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let mut data = snapshot_info.try_borrow_mut_data()?;
    // Empty sim snapshot forces write_snapshot_from_sim to seed player tails from world spawn.
    write_snapshot_from_sim(&mut data, &pirate_sim::snapshot::Snapshot::default(), world, m.player_count);

    if let Some(s) = header_mut(&mut data) {
        s.tick = 0;
        s.player_count = m.player_count;
        s.enemy_count = 0;
        s.projectile_count = 0;
        s.storm_radius_pct = 100;
        s.match_timer_sec = 0;
    }

    Ok(())
}
