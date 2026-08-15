use anchor_lang::prelude::*;

pub const MAX_PLAYERS: usize = 12;
pub const MAX_CONFIG_BYTES: usize = 8192;
pub const EVENT_RING: usize = 32;

#[account(zero_copy(unsafe))]
#[repr(C)]
pub struct GameConfigAccount {
    pub authority: Pubkey,
    pub version: u32,
    pub lobby_capacity_default: u8,
    pub tick_hz: u8,
    pub convoy_buff_cap_pct: u8,
    pub _pad: u8,
    pub data_len: u16,
    pub data: [u8; MAX_CONFIG_BYTES],
}

impl GameConfigAccount {
    pub const LEN: usize = 8 + core::mem::size_of::<GameConfigAccount>();
}

#[account]
#[derive(InitSpace)]
pub struct PlayerProfile {
    pub owner: Pubkey,
    pub gold: u64,
    pub contribution_score: u64,
    pub runs_completed: u32,
    pub hull_unlocked: u64,
    pub bump: u8,
}

/// Per-player velocity state (centi-units/s). Positions live in snapshot tail.
#[derive(Clone, Copy, Default)]
#[repr(C)]
pub struct ShipSimSlot {
    pub vel_x: i32,
    pub vel_z: i32,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default, InitSpace)]
pub struct PlayerSlot {
    pub owner: Pubkey,
    pub session_key: Pubkey,
    pub ready: u8,
    pub hull_id: u8,
    pub gold_earned: u32,
    pub kills: u32,
    pub damage_dealt: u32,
    pub revives: u16,
    pub contribution: u32,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, PartialEq, Eq, InitSpace)]
#[repr(u8)]
pub enum MatchState {
    Lobby = 0,
    Running = 1,
    Ended = 2,
}

/// Match header + player slots. Simulation state lives in linked Snapshot account.
#[account(zero_copy(unsafe))]
#[repr(C)]
pub struct MatchAccount {
    pub authority: Pubkey,
    pub seed: u64,
    pub tide: u8,
    pub capacity: u8,
    pub player_count: u8,
    pub state: u8,
    pub tick: u32,
    pub bump: u8,
    pub delegated: u8,
    pub vrf_pending: u8,
    pub players: [PlayerSlot; MAX_PLAYERS],
    pub ship_sim: [ShipSimSlot; MAX_PLAYERS],
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default, InitSpace)]
pub struct SnapshotPlayer {
    pub slot: u8,
    pub x: i16,
    pub z: i16,
    pub heading: u16,
    pub hp_pct: u8,
    pub level: u8,
    pub flags: u8,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default, InitSpace)]
pub struct SnapshotEnemy {
    pub id: u16,
    pub archetype: u8,
    pub x: i16,
    pub z: i16,
    pub hp_pct: u8,
    pub element: u8,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default, InitSpace)]
pub struct SnapshotProjectile {
    pub id: u16,
    pub x: i16,
    pub z: i16,
    pub vx: i16,
    pub vz: i16,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default, InitSpace)]
pub struct GameEvent {
    pub kind: u8,
    pub tick: u32,
    pub actor: u16,
    pub target: u16,
    pub x: i16,
    pub z: i16,
    pub value: u16,
}

/// Quantised broadcast state — sized at create_match from capacity + tide.
#[account(zero_copy(unsafe))]
#[repr(C)]
pub struct SnapshotAccount {
    pub match_key: Pubkey,
    pub tick: u32,
    pub player_count: u8,
    pub enemy_count: u8,
    pub projectile_count: u8,
    pub event_head: u8,
    pub max_enemies: u16,
    pub max_players: u8,
    pub bump: u8,
    // Variable tail: players[max_players], enemies[max_enemies], projectiles[64], events[32]
    // Stored in remaining bytes allocated at init.
}

impl MatchAccount {
    pub const LEN: usize = 8 + std::mem::size_of::<MatchAccount>();
}

pub fn snapshot_space(capacity: u8, max_enemies: u16) -> usize {
    let players = capacity as usize * std::mem::size_of::<SnapshotPlayer>();
    let enemies = max_enemies as usize * std::mem::size_of::<SnapshotEnemy>();
    let projectiles = 64 * std::mem::size_of::<SnapshotProjectile>();
    let events = EVENT_RING * std::mem::size_of::<GameEvent>();
    8 + std::mem::size_of::<SnapshotAccount>() + players + enemies + projectiles + events
}
