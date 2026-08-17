use anchor_lang::prelude::*;
use crate::delegation::delegate_pda;
use crate::errors::PirateCoopError;
use crate::state::MatchState;
use crate::ID;

/// Delegate the match PDA only (sets `delegated` before CPI).
#[derive(Accounts)]
pub struct DelegateMatchAccount<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    /// CHECK: match PDA; mutated then delegated via CPI
    #[account(mut)]
    pub match_account: UncheckedAccount<'info>,
    /// CHECK: pirate_coop program id (PDA owner before delegation)
    #[account(address = ID)]
    pub owner_program: UncheckedAccount<'info>,
    /// CHECK: ["buffer", match] PDA, owned by pirate_coop
    #[account(mut)]
    pub buffer: UncheckedAccount<'info>,
    /// CHECK: ["delegation", match] PDA on the MagicBlock delegation program
    #[account(mut)]
    pub delegation_record: UncheckedAccount<'info>,
    /// CHECK: ["delegation-metadata", match]
    #[account(mut)]
    pub delegation_metadata: UncheckedAccount<'info>,
    /// CHECK: MagicBlock delegation program
    #[account(address = ephemeral_rollups_sdk::id())]
    pub delegation_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

pub fn handler_match_account(ctx: Context<DelegateMatchAccount>) -> Result<()> {
    require_keys_eq!(*ctx.accounts.match_account.owner, ID);

    let (authority, seed) = {
        let mut data = ctx.accounts.match_account.try_borrow_mut_data()?;
        const STATE_OFFSET: usize = 8 + 44;
        const DELEGATED_OFFSET: usize = 8 + 53;
        require!(data.len() > DELEGATED_OFFSET, PirateCoopError::NotRunning);
        require!(
            data[STATE_OFFSET] == MatchState::Running as u8,
            PirateCoopError::NotRunning
        );
        data[DELEGATED_OFFSET] = 1;
        let authority = Pubkey::new_from_array(data[8..40].try_into().unwrap());
        let seed = u64::from_le_bytes(data[40..48].try_into().unwrap());
        (authority, seed)
    };

    let seed_bytes = seed.to_le_bytes();
    delegate_pda(
        &ctx.accounts.payer.to_account_info(),
        &ctx.accounts.match_account.to_account_info(),
        &ctx.accounts.owner_program.to_account_info(),
        &ctx.accounts.buffer.to_account_info(),
        &ctx.accounts.delegation_record.to_account_info(),
        &ctx.accounts.delegation_metadata.to_account_info(),
        &ctx.accounts.delegation_program.to_account_info(),
        &ctx.accounts.system_program.to_account_info(),
        &[b"match", authority.as_ref(), seed_bytes.as_ref()],
    )?;
    Ok(())
}

/// Delegate the snapshot PDA (one BPF heap copy per transaction).
#[derive(Accounts)]
pub struct DelegateSnapshotAccount<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    /// CHECK: match pubkey for snapshot seeds
    pub match_account: UncheckedAccount<'info>,
    /// CHECK: snapshot PDA; delegated via CPI
    #[account(mut)]
    pub snapshot: UncheckedAccount<'info>,
    #[account(address = ID)]
    pub owner_program: UncheckedAccount<'info>,
    #[account(mut)]
    pub buffer: UncheckedAccount<'info>,
    #[account(mut)]
    pub delegation_record: UncheckedAccount<'info>,
    #[account(mut)]
    pub delegation_metadata: UncheckedAccount<'info>,
    #[account(address = ephemeral_rollups_sdk::id())]
    pub delegation_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

pub fn handler_snapshot_account(ctx: Context<DelegateSnapshotAccount>) -> Result<()> {
    require_keys_eq!(*ctx.accounts.snapshot.owner, ID);
    let match_key = ctx.accounts.match_account.key();
    delegate_pda(
        &ctx.accounts.payer.to_account_info(),
        &ctx.accounts.snapshot.to_account_info(),
        &ctx.accounts.owner_program.to_account_info(),
        &ctx.accounts.buffer.to_account_info(),
        &ctx.accounts.delegation_record.to_account_info(),
        &ctx.accounts.delegation_metadata.to_account_info(),
        &ctx.accounts.delegation_program.to_account_info(),
        &ctx.accounts.system_program.to_account_info(),
        &[b"snapshot", match_key.as_ref()],
    )?;
    Ok(())
}

/// Delegate the world PDA (one BPF heap copy per transaction).
#[derive(Accounts)]
pub struct DelegateWorldAccount<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    /// CHECK: match pubkey for world seeds
    pub match_account: UncheckedAccount<'info>,
    /// CHECK: world PDA; delegated via CPI
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
    #[account(address = ID)]
    pub owner_program: UncheckedAccount<'info>,
    #[account(mut)]
    pub buffer: UncheckedAccount<'info>,
    #[account(mut)]
    pub delegation_record: UncheckedAccount<'info>,
    #[account(mut)]
    pub delegation_metadata: UncheckedAccount<'info>,
    #[account(address = ephemeral_rollups_sdk::id())]
    pub delegation_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

pub fn handler_world_account(ctx: Context<DelegateWorldAccount>) -> Result<()> {
    require_keys_eq!(*ctx.accounts.world.owner, ID);
    let match_key = ctx.accounts.match_account.key();
    delegate_pda(
        &ctx.accounts.payer.to_account_info(),
        &ctx.accounts.world.to_account_info(),
        &ctx.accounts.owner_program.to_account_info(),
        &ctx.accounts.buffer.to_account_info(),
        &ctx.accounts.delegation_record.to_account_info(),
        &ctx.accounts.delegation_metadata.to_account_info(),
        &ctx.accounts.delegation_program.to_account_info(),
        &ctx.accounts.system_program.to_account_info(),
        &[b"world", match_key.as_ref()],
    )?;
    Ok(())
}
