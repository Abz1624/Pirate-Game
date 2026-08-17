use anchor_lang::prelude::*;
use crate::delegation::commit_and_undelegate_match;
use crate::state::{MatchAccount, MatchState, SnapshotAccount};
use pirate_sim::world_from_bytes;

#[derive(Accounts)]
pub struct EndMatch<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,
    #[account(mut)]
    pub match_account: AccountLoader<'info, MatchAccount>,
    #[account(mut)]
    pub snapshot: AccountLoader<'info, SnapshotAccount>,
    /// CHECK: world PDA
    #[account(mut)]
    pub world: UncheckedAccount<'info>,
    /// CHECK: MagicBlock magic context PDA
    pub magic_context: UncheckedAccount<'info>,
    /// CHECK: MagicBlock magic program
    pub magic_program: UncheckedAccount<'info>,
}

pub fn handler(ctx: Context<EndMatch>) -> Result<()> {
    let mut m = ctx.accounts.match_account.load_mut()?;

    if let Ok(data) = ctx.accounts.world.try_borrow_data() {
        if let Some(world) = world_from_bytes(&data[8..8 + pirate_sim::WORLD_STATE_LEN]) {
            let cap = m.player_count as usize;
            for i in 0..cap.min(crate::state::MAX_PLAYERS) {
                let ps = &world.players[i];
                m.players[i].gold_earned = ps.gold.min(u32::MAX) as u32;
                m.players[i].kills = ps.player_kills as u32;
                m.players[i].damage_dealt = ps.damage_dealt;
                m.players[i].contribution = ps.bank.min(u32::MAX) as u32;
            }
        }
    }

    m.state = MatchState::Ended as u8;

    if m.delegated == 1 {
        commit_and_undelegate_match(
            &ctx.accounts.payer.to_account_info(),
            &ctx.accounts.magic_context.to_account_info(),
            &ctx.accounts.magic_program.to_account_info(),
            &ctx.accounts.match_account.to_account_info(),
            &ctx.accounts.snapshot.to_account_info(),
            &ctx.accounts.world.to_account_info(),
        )?;
        m.delegated = 0;
    }
    Ok(())
}
