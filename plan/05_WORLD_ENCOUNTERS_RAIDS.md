# 05 — World, Encounters, Bosses and Raids

## Geometry and discovery

- 48 m cells; home sides 144/240/336 m for 3×3/5×5/7×7 Zones. Four section families with three variants each.
- Section records contain connectors, obstacle polygons, encounter/POI/cosmetic/spawn/boss anchors and clearance data.
- Minimum corridor 6 m; boss clear radius 18 m. Validate one connected component and two outward routes where supported.
- One stage has 12 chests +4 POIs; home 10+4; raids have `12+4×extraZones` chests and `4+2×extraZones` POIs. Allocate 40% to highest Zone.
- Beacon interaction: 3 m, 0.75 s hold, movement stopped, damage interrupts. Discover within 24 m; sector at 7:00; exact marker at 9:00.
- Portal: 3 m/0.75 s, damage interrupt, remains marked and reachable.
- Dread boundary: 0.75 s grace; 6% max Hull/s until 2 s, 12% until 4 s, then 25%; one second inside resets.

## POIs

Guarantee Repair Wreck and Powder Cache; draw other distinct types. Repair heals 25%. Powder Cache spawns six ordinary + one elite and gives one run Item +20 Gold. Signal Buoy charges eight active seconds in 4 m and offers three saved stat choices; 10% gilded doubles increments. Smuggler's Oath grants `60×biome×depth` Gold and adds +10% new-enemy Hull/damage. Davy Jones adds +35% boss Hull/+15% damage for an extra boss Item chest. Salvage Bell pulls existing XP. Castaway Idol gives one saved temporary blessing. Treasure Chart reveals three nearest unopened chests. Default hold is 0.75 s within 2 m; commit consume+reward atomically.

## Enemy and boss encounters

Base enemies: Shark 18 Hull/4.4 speed/6 contact; Cannonboat 36/2.4/9 shot; Swordfish 25/3.5/12 charge; Hammerhead 100/2.8/18 charge; Manta 45/3.2/10 fan; Crab 90/1.8/30 armor/14 slam; Kraken Spawn 120/1.5/16 circles. Their XP/Gold are 1/1, 2/2, 2/2, 4/4, 2/2, 3/3, 5/5.

Gravewater uses drowned/spectral variants plus Siren; Devil's Maw uses fire/hazard variants plus Fire Eel. Preserve base telegraph duration unless a record explicitly changes it. Spawn only in a 24–36 m offscreen annulus, never within 8 m, terrain or visible safe area.

Bosses have phases at 65% and 30% and finish the current telegraph before transition:

- Barnacle King: 1,500 Hull/15 armor; Slam, Volley, Crab Call, phase-3 Charge; zero armor after Slam/Charge.
- Drowned Admiral: 1,800 Hull/10 armor; Ghost Broadside, Cursed Wake, Fog Order, Crossfire; forward/aft or alternating safe lanes remain.
- Infernal Kraken: 2,200 Hull/20 armor, stationary body; Tentacle Slam, Burning Rings with 70° gaps, Drag Current, Infernal Sweep; one safe sector remains.

Boss entrances last 2 s and are invulnerable while the player can move/attack. Leaving 30 m causes pursuit where possible, never reset/heal.

## Defense Director

Each Zone has three section-bound Encounter Slots. Threat budgets are 10/12/14 plus `floor((defenseRank-1)/2)`. Cards: Shark Pack 8/cost2; Cannonboat 4/3; Swordfish 5/3; Hammerhead 2/4; Manta 4/3; Crab 4/2; Kraken Spawn 2/4. Patterns: Patrol +0, Ambush/Pincer/Reinforcements/Swarm +1. At most two authored encounters active. One marked slow-current or cannon-crossfire hazard/Zone from rank 4 costs 2. Boss traits: Adds cost3, Armor cost2, Wide Volley cost3. Publishing invalid/over-budget drafts leaves prior version active.

## Raid transaction

Show three eligible targets; refresh free after 30 s. Exclude self, current/recent Crew, shields and same target within 24 h. Match within one Zone and prefer power within 25%, widening to 50% after 10 s. Freeze layout/defense/quote at start, but recalculate available exposure at boss settlement. Atomic theft is minimum of quote, live exposure, rolling allowance and attacker capacity. Deduct uncollected production first. Start shield immediately when anything is stolen. If a new shield blocks all theft, refund fee from game ledger. Never charge for invalid generation.

## Tests

Ten-thousand-seed reachability; object allocation; interrupted holds; POI reconnect; attack safe lanes; boss phase thresholds; retirement; authored overlap queue; defense permutation fairness; concurrent raid settlement; shield race; capacity clipping; fee compensation; once-only theft.

