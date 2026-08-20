use anchor_lang::prelude::*;
use ephemeral_rollups_sdk::cpi::{delegate_account, DelegateAccounts, DelegateConfig};
use ephemeral_rollups_sdk::ephem::commit_and_undelegate_accounts;

use crate::UndelegateCallback;

pub fn delegate_config(validator: Pubkey) -> DelegateConfig {
    DelegateConfig {
        validator: Some(validator),
        ..Default::default()
    }
}

pub fn delegate_pda<'info>(
    payer: &AccountInfo<'info>,
    pda: &AccountInfo<'info>,
    owner_program: &AccountInfo<'info>,
    buffer: &AccountInfo<'info>,
    delegation_record: &AccountInfo<'info>,
    delegation_metadata: &AccountInfo<'info>,
    delegation_program: &AccountInfo<'info>,
    system_program: &AccountInfo<'info>,
    pda_seeds: &[&[u8]],
    validator: Pubkey,
) -> Result<()> {
    delegate_account(
        DelegateAccounts {
            payer,
            pda,
            owner_program,
            buffer,
            delegation_record,
            delegation_metadata,
            delegation_program,
            system_program,
        },
        pda_seeds,
        delegate_config(validator),
    )?;
    Ok(())
}

pub fn commit_and_undelegate_match<'info>(
    payer: &AccountInfo<'info>,
    magic_context: &AccountInfo<'info>,
    magic_program: &AccountInfo<'info>,
    match_account: &AccountInfo<'info>,
    snapshot: &AccountInfo<'info>,
    world: &AccountInfo<'info>,
) -> Result<()> {
    commit_and_undelegate_accounts(
        payer,
        vec![match_account, snapshot, world],
        magic_context,
        magic_program,
    )?;
    Ok(())
}

pub fn undelegate_callback(ctx: Context<UndelegateCallback>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    m.delegated = 0;
    Ok(())
}
