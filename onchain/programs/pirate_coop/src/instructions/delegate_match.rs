use anchor_lang::prelude::*;
use crate::delegation::delegate_pda;
use crate::errors::PirateCoopError;
use crate::state::MatchState;
use crate::ID;

/// Match and snapshot must be `UncheckedAccount`, not `AccountLoader`.
/// After `delegate_account` the PDAs are owned by the delegation program;
/// `AccountLoader::exit` still writes the 8-byte discriminator and that
/// store faults (`Access violation in input section ... size 8`).
#[derive(Accounts)]
pub struct DelegateMatch<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    /// CHECK: match PDA; mutated then delegated via CPI
    #[account(mut)]
    pub match_account: UncheckedAccount<'info>,
    /// CHECK: snapshot PDA; delegated via CPI
    #[account(mut)]
    pub snapshot: UncheckedAccount<'info>,
    /// CHECK: world PDA; delegated via CPI
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
    /// CHECK: pirate_coop program id (PDA owner before delegation)
    #[account(address = ID)]
    pub owner_program: UncheckedAccount<'info>,
    /// CHECK: ["buffer", match] PDA, owned by pirate_coop
    #[account(mut)]
    pub match_buffer: UncheckedAccount<'info>,
    /// CHECK: ["delegation", match] PDA on the MagicBlock delegation program
    #[account(mut)]
    pub match_delegation_record: UncheckedAccount<'info>,
    /// CHECK: ["delegation-metadata", match]
    #[account(mut)]
    pub match_delegation_metadata: UncheckedAccount<'info>,
    /// CHECK: ["buffer", snapshot] PDA, owned by pirate_coop
    #[account(mut)]
    pub snapshot_buffer: UncheckedAccount<'info>,
    /// CHECK: ["delegation", snapshot]
    #[account(mut)]
    pub snapshot_delegation_record: UncheckedAccount<'info>,
    /// CHECK: ["delegation-metadata", snapshot]
    #[account(mut)]
    pub snapshot_delegation_metadata: UncheckedAccount<'info>,
    /// CHECK: ["buffer", world] PDA, owned by pirate_coop
    #[account(mut)]
    pub world_buffer: UncheckedAccount<'info>,
    /// CHECK: ["delegation", world]
    #[account(mut)]
    pub world_delegation_record: UncheckedAccount<'info>,
    /// CHECK: ["delegation-metadata", world]
    #[account(mut)]
    pub world_delegation_metadata: UncheckedAccount<'info>,
    /// CHECK: MagicBlock delegation program
    #[account(address = ephemeral_rollups_sdk::id())]
    pub delegation_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

pub fn handler(ctx: Context<DelegateMatch>, validator: Pubkey) -> Result<()> {
    require_keys_eq!(*ctx.accounts.match_account.owner, ID);
    require_keys_eq!(*ctx.accounts.snapshot.owner, ID);
    require_keys_eq!(*ctx.accounts.world.owner, ID);

    let match_key = ctx.accounts.match_account.key();
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

    let match_info = ctx.accounts.match_account.to_account_info();
    let snapshot_info = ctx.accounts.snapshot.to_account_info();
    let world_info = ctx.accounts.world.to_account_info();

    let payer = ctx.accounts.payer.to_account_info();
    let owner_program = ctx.accounts.owner_program.to_account_info();
    let delegation_program = ctx.accounts.delegation_program.to_account_info();
    let system_program = ctx.accounts.system_program.to_account_info();
    let match_buffer = ctx.accounts.match_buffer.to_account_info();
    let match_record = ctx.accounts.match_delegation_record.to_account_info();
    let match_metadata = ctx.accounts.match_delegation_metadata.to_account_info();
    let snapshot_buffer = ctx.accounts.snapshot_buffer.to_account_info();
    let snapshot_record = ctx.accounts.snapshot_delegation_record.to_account_info();
    let snapshot_metadata = ctx.accounts.snapshot_delegation_metadata.to_account_info();
    let world_buffer = ctx.accounts.world_buffer.to_account_info();
    let world_record = ctx.accounts.world_delegation_record.to_account_info();
    let world_metadata = ctx.accounts.world_delegation_metadata.to_account_info();

    let seed_bytes = seed.to_le_bytes();
    delegate_pda(
        &payer,
        &match_info,
        &owner_program,
        &match_buffer,
        &match_record,
        &match_metadata,
        &delegation_program,
        &system_program,
        &[b"match", authority.as_ref(), seed_bytes.as_ref()],
        validator,
    )?;

    delegate_pda(
        &payer,
        &snapshot_info,
        &owner_program,
        &snapshot_buffer,
        &snapshot_record,
        &snapshot_metadata,
        &delegation_program,
        &system_program,
        &[b"snapshot", match_key.as_ref()],
        validator,
    )?;

    delegate_pda(
        &payer,
        &world_info,
        &owner_program,
        &world_buffer,
        &world_record,
        &world_metadata,
        &delegation_program,
        &system_program,
        &[b"world", match_key.as_ref()],
        validator,
    )?;

    Ok(())
}
