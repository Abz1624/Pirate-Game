# Playtest feedback integration (2026-03-24)

## Balance
- `SaltwindSoloStageBalanceProfile.asset`: `balanceVersion` **3**, `levelZeroToOneXpCost` **5**, `logbookSlotCap` **4**, weapon pity levels **{2,3,5,8,12}**.
- Rarity base weights **60 / 26 / 10 / 3.2 / 0.8** with legendary cap **6%** (`RunRng`).

## Verification run
- EditMode: `unity command run_tests --mode EditMode` (live Editor; close Editor before `unity test` batch).
- Re-export profile hash via Balance Lab menu after asset edits.

## Additive components (see `PROTECTED_SETTINGS.json`)
- Boost, camera zoom, external forces, shake, SFX placeholders, day/night, wind gusts, spawn telegraph HUD, onboarding director, runtime ocean current + whirlpools.
