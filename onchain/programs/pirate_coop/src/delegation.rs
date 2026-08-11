use anchor_lang::prelude::*;
use ephemeral_rollups_sdk::anchor::{commit, delegate};
use ephemeral_rollups_sdk::cpi::DelegateConfig;
use ephemeral_rollups_sdk::ephem::MagicIntentBundleBuilder;

use crate::state::{MatchAccount, SnapshotAccount};
use crate::{UndelegateCallback, LOCAL_ER_VALIDATOR};

pub const UNDELEGATE_DISCRIMINATOR: [u8; 8] = [196, 28, 41, 206, 48, 37, 51, 167];

#[delegate]
#[derive(Accounts)]
pub struct DelegateMatchAccounts<'info> {
    pub payer: Signer<'info>,
    #[account(mut, del)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut, del)]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
}

pub fn delegate_config() -> DelegateConfig {
    DelegateConfig {
        validator: Some(LOCAL_ER_VALIDATOR),
        ..Default::default()
    }
}

pub fn commit_and_undelegate_match<'info>(
    payer: AccountInfo<'info>,
    magic_context: AccountInfo<'info>,
    magic_program: AccountInfo<'info>,
    match_account: AccountInfo<'info>,
    snapshot: AccountInfo<'info>,
) -> Result<()> {
    MagicIntentBundleBuilder::new(payer, magic_context, magic_program)
        .commit_and_undelegate(&[match_account, snapshot])
        .build_and_invoke()?;
    Ok(())
}

pub fn undelegate_callback(ctx: Context<UndelegateCallback>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;
    m.delegated = 0;
    Ok(())
}
