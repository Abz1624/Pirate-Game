use anchor_lang::prelude::*;
use crate::errors::PirateCoopError;
use crate::state::{snapshot_space, GameConfigAccount, MatchAccount, MatchState, SnapshotAccount};

#[derive(Accounts)]
#[instruction(seed: u64, tide: u8, capacity: u8, mode: u8)]
pub struct CreateMatch<'info> {
    #[account(mut)]
    pub authority: Signer<'info>,
    #[account(seeds = [b"config"], bump)]
    pub config: AccountLoader<'info, GameConfigAccount>,
    #[account(
        init,
        payer = authority,
        space = MatchAccount::LEN,
        seeds = [b"match", authority.key().as_ref(), seed.to_le_bytes().as_ref()],
        bump
    )]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(
        init,
        payer = authority,
        space = snapshot_space(capacity.max(1), tide_enemy_cap(tide)),
        seeds = [b"snapshot", match_account.key().as_ref()],
        bump
    )]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    pub system_program: Program<'info, System>,
}

fn tide_enemy_cap(tide: u8) -> u16 {
    (60 + tide as u16 * 20).min(96)
}

pub fn handler(
    ctx: Context<CreateMatch>,
    seed: u64,
    tide: u8,
    capacity: u8,
    mode: u8,
) -> Result<()> {
    let cap = if capacity == 0 {
        ctx.accounts.config.load()?.lobby_capacity_default
    } else {
        capacity
    };
    require!(cap > 0 && cap <= 12, PirateCoopError::InvalidCapacity);

    let mut m = ctx.accounts.match_account.load_init()?;
    m.authority = ctx.accounts.authority.key();
    m.seed = seed;
    m.tide = tide;
    m.capacity = cap;
    m.mode = mode;
    m.player_count = 0;
    m.state = MatchState::Lobby as u8;
    m.tick = 0;
    m.delegated = 0;
    m.vrf_pending = 0;
    m.players = [Default::default(); crate::state::MAX_PLAYERS];

    let mut s = ctx.accounts.snapshot.load_init()?;
    s.match_key = ctx.accounts.match_account.key();
    s.tick = 0;
    s.max_players = cap;
    s.max_enemies = tide_enemy_cap(tide);
    s.player_count = 0;
    s.enemy_count = 0;
    s.projectile_count = 0;
    s.event_head = 0;
    s.storm_radius_pct = 100;
    s.match_timer_sec = 0;
    s.orb_count = 0;

    Ok(())
}
