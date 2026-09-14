# 04 — Content Catalogue

Every record includes stable ID, schema/content version, localization keys, modes, release phase, tags, prerequisites, visuals/audio, numeric behavior and validation fixtures.

## Launch Weapons

- `weapon.deck_cannon`: 24 damage/1.2 s/22 m; 24 m/s; +12% damage and +3% speed/rank; rank 4/8/12 adds pierce/projectile/pierce.
- `weapon.broadside`: two shots/side, 14 damage/1.8 s; 18 m/s; +10% damage/+3% speed; milestones add one shot/side.
- `weapon.powder_kegs`: 42 damage/2.4 s, 1.8 s fuse, 3.5 m; max 12 active; milestones second keg/1.4 s fuse/third keg.
- `weapon.flying_cutlasses`: two blades, 12 damage, 2.5 m orbit, 0.4 s per-target interval; +9% damage/+3% angular speed; milestones add blades.
- `weapon.flamethrower`: 55°×6 m cone, 5 damage/0.2 s, 1.8 s active +1.2 s recovery; milestones width/duration/duration.
- `weapon.lightning_mast`: 32 damage/1.7 s/18 m, two jumps at 80%, 6 m jump; milestones jump/jump/85% retention.

All max at rank 12. Expansion Weapons Harpoon, Mortar, Anchor Flail, Ghost Cannons, Chainshot and Siren Bell remain disabled until post-launch content approval.

## Logbooks

Launch foundational: Gunner +8% damage; Powdermaster +7.5% attack speed; Navigator +3% speed; Carpenter +10 Hull; Lookout +2 crit points; Quartermaster +8% generated run Gold; Scavenger +10% pickup radius; Cartographer +7% XP. Unlockable: Ironclad +2 armor; Fortune +5 Luck; Tidecaller +5% effect duration; Admiral +3% elite/boss damage; Plunderer +3% boss rewards; Chaos chooses one visible useful random increment. Max rank 40; all global caps apply.

## Items

- Common protected pool: Lucky Doubloon, Spare Planks, Gunner's Glove, Powder Monkey, Shark Tooth Necklace, Manta Sail, Reef Carapace, Crow's Nest.
- Uncommon: Skeleton Key, Grapeshot Pouch, Double-Fuse Cord, Serrated Scabbard, Dragon's Breath Nozzle, Copper Conductor, Broadside Drum, Hammerhead Helm.
- Rare: Smuggler's Ledger, Black Spot, Dead Man's Compass, Kraken Ink Sac, Cursed Coin.
- Epic: Storm in a Bottle, Devil's Match, Queen Anne's Fuse, Barnacle Crown, Jolly Roger.
- Legendary: Leviathan Heart, Captain's Last Rum.

Implement each Item's trigger, cap, proc coefficient, ancestry, rank magnitude, quest, Rum price and test as a separate content record. Persistent rank is 0–10 with `1+0.005r`, applied to one named magnitude only. Run stacks reset at death.

## Captains

- Rook: Cannon; +0.5% damage/level, cap 25%.
- Brine: Cannon; 110 Hull, 5.7 m/s; +0.5 armor/level, cap 25.
- Vale: Broadside; 90 Hull; +0.5% Gold/level cap 25%; damage `min(30%,runGold/2000)`.
- Flint: Kegs; 90 Hull, 6.3 m/s; moving >4 m/s for 2 s gives +15% Keg damage; +0.5%/level cap 25%.
- Cinder: Flamethrower; 90 Hull; +0.5% burn damage/level cap 30%; every 50th valid burning kill heals 5.
- Tempest: Mast; 85 Hull, 6.3 m/s; +0.5% lightning/level cap 30%; near miss gives +20% for 2 s, 6 s cooldown.

Prototype Rook/Brine/Flint first. Ship skins never change Captain behavior.

## Content validation

Reject duplicate/missing IDs, invalid weights, negative quantities, unreachable unlocks, incompatible tags, unbounded procs, missing low-VFX/audio/icon reference, unsupported modes and layouts above budget. Export manifest hash and retain old run versions.

