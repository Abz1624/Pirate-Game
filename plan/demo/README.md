# Demo build (30 Sep 2026): the art team's assets on V4

Branch `demo` (from `v5-live-game` after V5 M3, plus the art team's `gameplay` branch). V5 work is paused;
`v5-live-game` is untouched.

## What is wired in

| Area | What the game shows now | Where it is set |
|---|---|---|
| Main menu | The art team's `MainMenuPanel` over the 3D menu scene (islands and cursed brig) from `UI.unity`; Set Sail opens Captain select; the Rum counter shows the player's Rum. Leaderboard, Ledger, Raid, Settings and Profile are shown but do nothing yet. | `Assets/_Game/V4/Resources/V4ArtMenuSettings.asset` |
| Captain select | `CharacterSelectionPanel`: each slot picks that Captain, shows its details (ability name and text from the Captain config) and its ship; locked Captains show their lock and why; Set Sail starts the run, or reads "UNLOCK — n RUM" when Rum is all that is missing. | same |
| In-run HUD | `HUD_SaltWind`: Captain panel (portrait, name and level, hull and XP bars), timer, gold, kills, the objective banner (current goal, a skull per step done) and the weapon row with the new icons. Items, Logbooks, the minimap, boost, Captain active, level-up cards and results stay V4's. | `V4SaltWindHudSettings.asset` |
| Ship | The chosen Captain's sail and flag on the player ship. | `V4SaltWindHudSettings.asset` (per Captain) |
| Icons | The new weapon and item icons from `UI/New_items` everywhere the HUD shows them. | `V4IconCatalog.asset` |
| Level-up cards | The new rarity card backgrounds. | `_Game/Resources/LevelUpCardArtSettings.asset` |
| Landmarks | Salvage chests use the treasure chest; the Repair Wreck the repair wreck; the Treasure Chart the treasure map; the Powder Cache the cursed tombstone. | `_Game/Resources/DemoJuiceSettings.asset` (POI Visuals) |

**Re-run after changing the art:** Tools ▸ Rogue Pirates ▸ Demo ▸ Wire Art Team Assets. It refills all of the above from
the art files (and rebuilds the menu backdrops from `UI.unity`'s `MainMenuSetup` and `CharacterSelection Setup`). VFX
profile slots that were already set are kept.

## Editing VFX (for artists)

Tools ▸ Rogue Pirates ▸ **VFX Editor**. Pick a weapon, the player ship or an enemy on the left. Each moment has:

- **Prefab**: your effect. With **Replace default** on, it plays instead of the game's effect; off, on top of it.
- **Scale**: the size of your prefab, or, with no prefab, of the game's own effect.
- **Tint**: multiplies the effect's particle and material colours.
- **Offset**: x right, y up, z forward of the subject (or of the firing direction).
- **Lifetime**: seconds before it is returned (0 = as long as its particles last).

| Subject | Moments |
|---|---|
| Weapon | Muzzle (on firing), Projectile (the projectile model), Trail (on each projectile), Hit (where it hits), Death (when it kills; an enemy's own death effect wins) |
| Player ship | Spawn (run start), Trail (follows the ship), Hit (taking damage), Death (sinking) |
| Enemy | Spawn, Trail (follows it), Hit (when hit), Death |

Changes save at once and apply live in Play mode. **Preview** (Play mode) fires the moment in front of the ship.
Examples already set: the Barnacle King arrives in a tornado and burns when it dies; the Kraken Spawn and the Reef Crab
rise out of whirlpools (the crab burns green); the ship burns when it sinks. The whirlpool and tornado are decoration
only (V4 has no whirlpool or tornado hazard).

"Add missing entries" adds a profile for any new weapon or enemy. The data is `Assets/_Game/V4/Resources/V4VfxProfiles.asset`.

## Not done (art without a V4 feature yet)

The art's own level-up cards (`UpgradeSelection`) and item grid, the art minimap frame, the stat bars on Captain select
(V4 has no handling or firepower stats), the bell (no prefab path for floor powerups), boat and bridge (FBX only, no
prefab), and the islands with cannons.
