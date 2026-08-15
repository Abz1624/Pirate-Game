use anchor_lang::prelude::*;
use ephemeral_rollups_sdk::anchor::ephemeral;

pub mod delegation;
pub mod errors;
pub mod instructions;
pub mod ship_sim;
pub mod snapshot_tail;
pub mod state;

use instructions::*;

declare_id!("EeWGiecEDWgBB3eUxgQfTLjQJcZnLRYsXvbCcYfxP6pC");

/// Local ER validator identity (MagicBlock localnet).
pub const LOCAL_ER_VALIDATOR: Pubkey = pubkey!("mAGicPQYBMvcYveUZA5F5UNNwyHvfYh5xkLS2Fr1mev");

#[ephemeral]
#[program]
pub mod pirate_coop {
    use super::*;

    pub fn initialize_config(ctx: Context<InitializeConfig>, version: u32) -> Result<()> {
        instructions::initialize_config::handler(ctx, version)
    }

    pub fn update_config(ctx: Context<UpdateConfig>, data: Vec<u8>) -> Result<()> {
        instructions::update_config::handler(ctx, data)
    }

    pub fn create_match(
        ctx: Context<CreateMatch>,
        seed: u64,
        tide: u8,
        capacity: u8,
    ) -> Result<()> {
        instructions::create_match::handler(ctx, seed, tide, capacity)
    }

    pub fn delegate_match(ctx: Context<DelegateMatch>) -> Result<()> {
        instructions::delegate_match::handler(ctx)
    }

    pub fn join_match(ctx: Context<JoinMatch>, session_key: Pubkey) -> Result<()> {
        instructions::join_match::handler(ctx, session_key)
    }

    pub fn set_ready(ctx: Context<SetReady>, ready: bool) -> Result<()> {
        instructions::set_ready::handler(ctx, ready)
    }

    pub fn start_match(ctx: Context<StartMatch>) -> Result<()> {
        instructions::start_match::handler(ctx)
    }

    pub fn submit_input(
        ctx: Context<SubmitInput>,
        throttle: i8,
        steer: i8,
        buttons: u8,
    ) -> Result<()> {
        instructions::submit_input::handler(ctx, throttle, steer, buttons)
    }

    pub fn tick_phase1(ctx: Context<TickPhase>) -> Result<()> {
        instructions::tick::phase1(ctx)
    }

    pub fn tick_phase2(ctx: Context<TickPhase>) -> Result<()> {
        instructions::tick::phase2(ctx)
    }

    pub fn tick_phase3(ctx: Context<TickPhase>) -> Result<()> {
        instructions::tick::phase3(ctx)
    }

    pub fn tick_phase4(ctx: Context<TickPhase>) -> Result<()> {
        instructions::tick::phase4(ctx)
    }

    pub fn draft_pick(ctx: Context<DraftPick>, card_index: u8, vrf_seed: [u8; 32]) -> Result<()> {
        instructions::draft::handler(ctx, card_index, vrf_seed)
    }

    pub fn end_match(ctx: Context<EndMatch>) -> Result<()> {
        instructions::end_match::handler(ctx)
    }

    pub fn credit_profile(ctx: Context<CreditProfile>) -> Result<()> {
        instructions::credit_profile::handler(ctx)
    }

    /// Undelegate callback discriminator handler required by delegation program.
    pub fn undelegate_callback(ctx: Context<UndelegateCallback>) -> Result<()> {
        delegation::undelegate_callback(ctx)
    }
}

#[derive(Accounts)]
pub struct UndelegateCallback<'info> {
    #[account(mut)]
    pub match_account: AccountLoader<'info, state::MatchAccount>,
}
