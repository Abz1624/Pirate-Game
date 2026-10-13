# Rogue Pirates: pending art (13 Oct 2026)

What the art team still has to make or fix, checked against what is in the game today (branch `v5-live-game`, after the
hackathon build of 13 Oct). The full briefs and GPT prompts for each piece are in the Art Guide (`plan/hackathon/art-guide.md`);
this list says what is still open.

**Status:** **missing** (no art; the game shows a name, a generic glyph or nothing), **placeholder** (a primitive, a tint, a
borrowed model, icon or effect stands in), **needs fix** (your art is in the game but needs a change).
Art that exists but is not plugged into the game yet is our job, listed separately at the end.

**Priority:** **P0** players see a placeholder today, or the content is being switched back on now;
**P1** needed soon (art over the phone budget, content in play that looks unfinished, next phase); **P2** later phases.
**P0 (ours)**: a placeholder shows today, but your art for it is already delivered; we only have to plug it in (section 9).

## Technical rules (apply to everything below)

| Rule | Spec |
| --- | --- |
| Content icons (weapons, items, Logbooks, evolutions, powerups) | 1024 × 1024 PNG master, transparent background, readable at 64 px; the game imports it at 256-512. Items sit on the rarity frame (Common grey, Uncommon green, Rare blue, Epic purple, Legendary gold). Logbooks: one family style, a leather book with a stat emblem, cover colour by group (offence red, defence blue, movement teal, economy gold) |
| UI kit sprites (glyphs, panels, badges) | 1024 masters; imported at 256 (icons) or 512 (panels). White on transparent when the game tints it. Panels are 9-slice: mark the slice border. Same parchment style and naming as the kit you delivered (`Icon_...`, `Panel_...`, `Btn_...`) |
| Models | Stylized low-poly, one material where possible, URP Unlit or Simple Lit, no lights, effects and pickups cast no shadows. Pivot at the waterline centre, +Z forward, 1 unit = 1 m. LOD0 plus a 50% LOD1 for anything over 2,000 triangles |
| Triangle budgets | Ordinary enemy 300-800; specialist 600-1,200; elite or miniboss 1,500-2,500; boss 4,000-6,000; player ship 3,000-5,000; weapon mount ≤ 500; orbit or summon model ≤ 300 (turret ≤ 600); projectile ≤ 100 or a billboard; pickup ≤ 150; POI 1,000-2,500; decoration ≤ 1,500 (small ≤ 500); defence 800-1,500 |
| Textures | Power of two. Hero (player ship, boss) ≤ 1024; enemies and POIs ≤ 512; props and VFX sheets ≤ 256. Web builds cap every texture at 1024, so anything bigger is wasted download |
| VFX | Shuriken particles only (no VFX Graph: it draws nothing on phones). Muzzle ≤ 20 particles; projectile trail ≤ 8; hit ≤ 30 particles and ≤ 3 systems, lifetime ≤ 2 s; persistent effect (stream, aura, beam) ≤ 80 particles and ≤ 200 triangles; boss attack ≤ 150. Share one or two atlases, soft particles off, one-shots do not loop, effects reset cleanly when reused |
| Colours | Enemy warnings orange-red; player rewards warm (gold, cream); heal green; XP cyan; never red vs green alone (add a shape) |
| Delivery | Editable source, the export, and a screenshot at gameplay scale (top-down camera, phone size). No scripts in prefabs |

**Where to save:**

| What | Folder (under `Pirate Game Unity/Assets/`) | File name |
| --- | --- | --- |
| Weapon, item, Logbook icons | `_Game/V4/Demo/Art/Icons/Weapons/`, `.../Items/`, `.../Logbooks/` | the content id, e.g. `cursed_chest_key.png` |
| Evolution, powerup, affix, status icons | `_Game/V4/Demo/Art/Icons/Evolutions/`, `.../Powerups/`, `.../Affixes/`, `.../Status/` (new folders) | the id, e.g. `black_tide.png` |
| Menu and island screen sprites | `_Game/Hackathon/Art/UI/Kit/` | kit names, e.g. `Icon_Walls.png`, `Portrait_gunner_cog.png` |
| Models and VFX | your usual folders (`3D Models/Enemies/...`, `GameObject/Guns/<Weapon>/`) | any; tell us the file |

## 1. UI icons for game content (10 rows, plus the 19 evolution icons)

Icons for the 19 weapons, 47 items and 15 Logbooks in play are done and wired, apart from the rows below.

| Asset | What it is for in the game | Status | Pri |
| --- | --- | --- | --- |
| `squid_ink_pots` weapon icon | Squid Ink Pots card, HUD weapon row, shop. Being switched back on now | placeholder: shows the Kraken Ink Sack item icon (same picture as the item) | P0 |
| `siren_bell` weapon icon | Siren Bell card and HUD. Being switched back on now | placeholder: the "Aura" icon (Tidal Aura art) stands in; the weapon is a brass bell that pulses a slowing ring | P0 |
| `cursed_chest_key` item icon | Uncommon item that evolves Cursed Doubloons into Pirate King's Hoard. Being switched back on now | missing (kept out of the chests until it has one) | P0 |
| Evolution icons (19), listed in section 3 | Level-up card, HUD weapon row and collection when a weapon evolves: the base icon on an evolved frame with a new motif | placeholder: the base weapon's icon is reused | P0 |
| Powerup HUD buff icons (8): Repair Supplies, Powder Rush, Magnetic Tide, Dead Calm, Battle Signal, Tailwind, Salvage Chime, Full Sail | HUD while a floor powerup's buff runs | missing | P1 |
| Elite affix icons (6): Armored, Swift, Volatile, Rich, Cursed, Captain | Elite name plate and health bar | missing | P1 |
| Status effect icons (5): burn, slow, shock, bleed, curse | Over enemies and on the HUD | missing | P1 |
| Token currency icon | Shop and rewards (later) | missing (RUM and gold exist in the old set) | P2 |
| Minimap glyphs (about 12): ship, POIs, boss, chest, beacon | Minimap | partial (some in the old minimap sheet, the rest drawn in code) | P2 |
| Menu hub tab icons (6): Play, Shop, Inventory, Leaderboard, 1v1, Settings, 512 × 512 | Menu hub buttons | partial (text buttons plus kit glyphs) | P2 |

### Evolution icons (19, all P0, same spec as content icons)

| id | Evolution | Base weapon + item | Look |
| --- | --- | --- | --- |
| stormshot_cannon | Stormshot Cannon | Deck Cannons + Grapeshot Pouch | gold + electric blue |
| kings_broadside | King's Broadside | Broadside Battery + Broadside Drum | gold, royal red |
| devils_wake | Devil's Wake | Powder Kegs + Double-Fuse Cord | hellfire red |
| bladed_tide | Bladed Tide | Flying Cutlasses + Serrated Scabbard | steel, foam |
| hellfire_nozzle | Hellfire Nozzle | Flamethrower + Dragon's Breath Nozzle | orange, blue core |
| tempest_mast | Tempest Mast | Lightning Mast + Copper Conductor | violet-white |
| kraken_chain | Kraken Chain | Anchor Flail + Rusted Shackle | iron, kraken purple |
| bar_shot_barrage | Bar Shot Barrage | Chainshot + Chain Locker | iron, white blur |
| drowned_choir | Drowned Choir | Siren Bell + Siren Scale | ghost teal |
| black_tide | Black Tide | Squid Ink Pots + Kraken Ink Sack | ink black, violet |
| whalers_volley | Whaler's Volley | Harpoon Gun + Whale Oil Barrel | tan, oil black |
| ship_of_the_line | Ship of the Line | Long Nine + Powder Horn of the Line | amber |
| siege_bombard | Siege Bombard | Mortar + Siege Quadrant | yellow, iron |
| swivel_battery | Swivel Battery | Swivel Guns + Swivel Mount | pale gold |
| parrot_armada | Parrot Armada | Parrot Squadron + Captain's Perch | green, red |
| monkey_battery | Monkey Battery | Monkey Gunners + Banana Crate | brown, banana yellow |
| abyssal_vortex | Abyssal Vortex | Maelstrom Charge + Storm Conch | deep navy, white |
| beacon_of_the_deep | Beacon of the Deep | Lighthouse Lantern + Fresnel Lens | warm white, cyan rim |
| pirate_kings_hoard | Pirate King's Hoard | Cursed Doubloons + Cursed Chest Key | green-gold |

## 2. Weapons: models and effects (24 rows)

Columns: **projectile** (the shot or body), **muzzle** (on firing), **impact** (on hit), **area** (ring, slick, beam or
stream). "ok" means nothing pending for that part. Budgets per the technical rules.

| Weapon | In game | Projectile | Muzzle / mount | Impact | Area | Status | Pri |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Flamethrower | 9.5 m flame cone, 52°. Being switched back on now | - | ok | burning patch: the current one renders a broken pink material | stream redo (owner's VFX artist, R-89), ≤ 80 particles | needs fix | P0 |
| Siren Bell | Pulse around the ship every 3 s, 20% slow. Being switched back on now | - | bell on the ship, ≤ 400 triangles (the fish bell model is 8,650) | - | pulse ring, brass and sea-teal | placeholder: a plain area ring | P0 |
| Squid Ink Pots | Lobbed pots leave a 3 s slick that slows. Being switched back on now | your Ink Pot is wired | - | landing splash ≤ 12 particles (in your pack, see last section) | ink slick decal (in your pack, see last section) | placeholder: violet rings drawn in code | P0 (ours) |
| Lighthouse Lantern | A 9 m beam sweeps around the ship. Being switched back on now | your lanterns are wired | - | - | beam ≤ 20 particles, warm white: check it reads at 9 m when we switch it on | check | P1 |
| Maelstrom Charge | A 3 s whirlpool drags enemies, then implodes | - | - | implode burst ≤ 30 | whirlpool ≤ 60 particles, Shuriken | placeholder: a timed ring (your Maelstrom pack is not wired yet) | P0 (ours) |
| Mortar | Shell on the densest cluster after 0.6 s | ok | ok | ok | friendly yellow landing marker | placeholder: Lightning Mast effect tinted red (your Mortar Barrage pack may cover it) | P0 |
| Monkey Gunners | Floating turret astern fires within 10 m | shot: code capsule (orange) | muzzle ≤ 10 particles | small hit | - | placeholder (the monkey itself is your model) | P0 |
| Parrot Squadron | Parrots dive at enemies | dive shot: code capsule (green) | - | small hit | - | placeholder (the parrots are your model) | P0 |
| Cursed Doubloons | Coins jump between enemies | cursed green-on-gold coin billboard, ≤ 8 particles | - | coin hit | jump trail | placeholder: the gold barrel's coin particle | P1 |
| Anchor Flail | Maces on a chain, thrown and reeled back | your anchor and chain links are wired | - | water spray ≤ 10 particles | - | ok apart from the spray | P2 |
| Chainshot | Wide piercing shot | your chainshot is wired | - | ok | - | done | - |
| Deck Cannons | Starter: forward shot | ok (cannonball) | mount ≤ 500 triangles; today's muzzle is a whole 11,992-triangle cannon | shared hit kit | - | needs fix (over budget) | P1 |
| Long Nine | Long forward lane shot | amber tracer | mount; today's muzzle is an 18,266-triangle model | shared hit kit | - | needs fix (over budget) | P1 |
| Lightning Mast | Chains 4 targets | - | - | strike ≤ 30 | chain effect: today 15 systems, up to 7,120 particles (budget ≤ 80) | needs fix (far over budget) | P1 |
| Powder Kegs | Mines astern | keg ≤ 100-300 (today 1,445) | - | ok | - | needs fix (over budget) | P1 |
| Flying Cutlasses | Two blades orbit | blade ≤ 300 (today 1,620) | - | ok | - | needs fix (over budget) | P1 |
| Harpoon Gun | Forward tether | ok | mount needed | ok | - | missing (mount) | P2 |
| Swivel Guns | Rapid small shots all round | ok | mount | ok | - | missing (mount) | P2 |
| Broadside Battery | Fan volleys both sides | ok | ok | ok | - | done | - |
| Shared hit kit | Every hit flashes, on every weapon | - | - | 3 sizes (≤ 8, ≤ 20, ≤ 30 particles), a crit variant, a burn flicker, a level-up burst, one atlas | - | placeholder: a borrowed fish damage effect (5 systems on every hit) | P0 |
| Kill burst (elites and boss) | Every elite and boss death | - | - | ≤ 30 particles | - | needs fix: today 17 systems | P1 |
| Telegraph kit | Enemy warnings and aura rings | - | - | strike flash | ring, filled disc, lane, wedge, fill-over-time, one shared material | placeholder: drawn in code | P1 |
| New item effects: Devil's Match (small blast on burning deaths), Hammerhead Helm (bow spark), Storm in a Bottle and Queen Anne's Fuse (reuse lightning and the keg blast) | Item procs when these items come back | - | - | small blast ≤ 20, spark ≤ 10 | - | missing | P1 |
| Weapon mounts for the other weapons (sockets at bow, sides, stern, mast) | Shows what the ship carries | - | ≤ 500 each | - | - | missing | P2 |

## 3. Evolutions: bodies and signature effects (1 row)

| Asset | What it is for | Status | Pri |
| --- | --- | --- | --- |
| A stronger body and one signature effect per evolution (19), within the base weapon's budget plus ≤ 20 particles (looks in the evolution icon table) | The evolved weapon in play | placeholder: the base weapon's effects are reused | P1 |

## 4. Enemies and bosses (12 rows)

13 enemy ships and the two crowned elites are your models and are in the game. Still open:

| Asset | What it is for | Status | Pri |
| --- | --- | --- | --- |
| Barnacle King model, ~6 m, 4,000-6,000 triangles, 1024 texture; entrance, idle, 4 attack wind-ups (spread, charge, bombard, shell slam), recover, phase change, enrage, death | Voyage boss and the island guardian you must sink in every raid | placeholder: the Reef Crab mesh scaled 2.4× | P0 |
| Bomb Boat model (fire orange sails), 600-1,200 triangles | Runs in and explodes | placeholder: shared hull, told apart by tint | P0 |
| Rammer Raft model (bone sails) | Telegraphed ram; also pickable in the defence designer | placeholder: shared 11,462-triangle ram ship | P0 |
| Treasure Raider model (gold sails) | Treasure-carrying variant | placeholder: shared hull | P0 |
| Boss arrival whirlpool, Shuriken, ≤ 150 particles | 2.5 s boss entrance | placeholder: the current one is VFX Graph and shows as a splash on phones | P0 |
| Elite kit: gold trim or crown, ground aura ring per affix colour | Every elite | missing (only the two crowned elites have a look) | P1 |
| Reef Crab lob shot and aim warning | Its big slow cannonball every 2.5 s after a 0.6 s aim | placeholder | P1 |
| Cannonboat model, 600-1,200 triangles | Ranged enemy | needs fix: 5,258 triangles | P1 |
| Kraken Spawn model | Slow tank, tentacle circles | needs fix: 6,724 triangles | P1 |
| Reef Crab miniboss: armour-down window look | Miniboss | missing state | P1 |
| Barnacle King death effect, ≤ 150 particles | Boss death | needs fix: today up to 4,000 particles | P1 |
| Later bosses: Drowned Admiral, Infernal Kraken | Later biomes | missing | P2 |

## 5. Points of interest, pickups and props (15 rows)

| Asset | What it is for | Status | Pri |
| --- | --- | --- | --- |
| Floor powerup models (8), ≤ 300 triangles, bob and spin, coloured ring on the water, pickup burst ≤ 20 particles | Powerups that drop in voyages | placeholder: code-built cubes, spheres and capsules (four are a grey sphere) | P0 |
| Powder Cache, 1,000-2,500 triangles, 5 states (unseen, idle, in range, active, used) | Elite ambush POI | placeholder: a pirate tombstone with fire (7,285 triangles) | P0 |
| Salvage Chest, ≤ 1,500 triangles, with an opened state | Paid chest, 12 per stage | needs fix: 77,650 triangles (the worst asset in the game) | P1 |
| Health, gold and XP barrels, ≤ 150 triangles each | Drops; the health barrel drops in view on a timer | needs fix: 1,752 triangles each | P1 |
| XP gem, ≤ 150 triangles | Up to 200 on screen | needs fix: 624 triangles | P1 |
| Treasury Beacon marker | Summons the boss in voyages | placeholder: a borrowed whirlpool (works on phones) | P1 |
| Repair Wreck | One-use heal | needs fix: 10,484 triangles | P1 |
| Treasure Chart | Reveals the 3 nearest chests | needs fix: 8,110 triangles | P1 |
| Player ship (Cutlass Sloop), 3,000-5,000 triangles, 1024 hull, 512 sail | The player | needs fix: 20,008 triangles | P1 |
| Signal Buoy, with a charge ring | Switched off for now | needs fix: 12,240 triangles | P2 |
| Lighthouse of Fortune | Not placed yet | needs fix: 11,597 triangles | P2 |
| Exit portal, Davy Jones' Contract, Salvage Bell, Castaway Idol | POIs not built yet | missing | P2 |
| Voyage island sections, 3,000-6,000 triangles with LOD1 | 45-50 islands per stage | needs fix: 8,000-20,000 each | P2 |
| Home island decorations (38) and defences (6), with 512 × 512 store thumbnails (about 44) | Island building phase | missing | P2 |
| Sails and flags canvas (512 × 512 sail, flag band 506 × 332, stickers 256 × 256), brand slots | Sail editor and bounty islands | missing | P2 |

## 6. UI kit gaps: menus, islands, raids and 1v1 (24 rows)

Sprites in the parchment kit style you delivered (UI kit rules above).

| Asset | What it is for in the game | Status | Pri |
| --- | --- | --- | --- |
| Enemy portraits (17), 256 × 256 on the kit's card: Deckhand Dinghy, Swordfish, Rammer Raft, Cutthroat Skiff, Hammerhead, Shark, Cannonboat, Needle Sloop, Bomb Boat, Manta Ray, Gunner Cog, Bomb Ketch, Boarding Barge, Kraken Spawn, Elite Hammerhead, Elite Kraken Spawn, Reef Crab | Defence designer: the enemy picker and each minute's rows | placeholder: a tinted card with a role glyph (swords, shield, skull) | P0 |
| Guardian portrait: Barnacle King, 256 × 256 | Defence designer guardian card, raid HUD | placeholder: crown and skull glyphs | P0 |
| Captain Marrow portrait, 256 × 256 | The AI captain's banner in 1v1 and the island defender | placeholder: one of the player captains' portraits | P0 |
| Walls building icon | Islands screen building rows | missing: a shield glyph stands in | P0 |
| Vault building icon | Islands screen building rows | placeholder: the kit's chest | P0 |
| RUM icon (barrel), 256 × 256 | RUM balance pill, RUM plank, raid loot, island rows | placeholder: the main menu's decorative barrel | P0 |
| SOL coin-stack icon, 256 × 256 | Wallet balance, market prices, wager stakes and pots, weekly rewards | placeholder: the kit's chest | P0 |
| Player avatars (a set of 8-12), 256 × 256, round | Leaderboards, daily race board, market seller, 1v1 lobby seats | missing: a generic user glyph | P0 |
| Island pictures, 1024 × 1024 (one per world slot (24) or per region) | Island cards and the Island NFT image (shown in wallets and explorers) | placeholder: circles drawn in code; the NFT image is a generated 256 px blob | P0 |
| Islands world map (sea chart), 2048 × 2048 master, imported at 1024 | The 24-slot world map on the Islands screen | placeholder: blue fill with grid lines drawn in code | P0 |
| Threat arrow: arrow 256 × 256, label plate 512 × 128 (9-slice), boss (skull) and elite (crown) variants | Screen edge, pointing at the off-screen boss, minibosses and elites | placeholder: a code-drawn triangle | P0 |
| Health bar: frame 512 × 96 (9-slice), fill strip, skull end-cap (boss), crown end-cap (elite) | Over elites, minibosses, the boss and the 1v1 opponent | placeholder | P0 |
| 1v1 opponent ship look: red sails, dark trim, black flag; nameplate 512 × 128 | The other player's ship | placeholder: the player's ship tinted red | P0 |
| 1v1 storm ring: band texture 512 × 128, lightning flecks ≤ 80 particles | The shrinking safe zone | placeholder: a violet line | P0 |
| 1v1 edge arrow glyphs (3), 128 × 128: ship, gem barrel, cross barrel | Arrows to the opponent, XP and health barrels | placeholder: code-drawn | P0 |
| Touch controls: joystick base 512, knob 256, boost button 256 with a wind icon, ability ring 256 | Phone controls | partial: the boost button reuses the joystick base | P1 |
| Leaderboard medals (gold, silver, bronze), 256 × 256; rank-row frame (9-slice) | Weekly board, daily race | placeholder: kit crowns and dots | P1 |
| Raid result badges: Won, Lost, Raided, Defended, 256 × 256 | Raid history rows | placeholder: kit tags with text | P1 |
| Island NFT minted badge and "listed" stamp | Islands and Market screens | placeholder: kit tags | P2 |
| VICTORY and DEFEAT banners 1024 × 384; rating up and down badge 256 × 256 | 1v1 end card | placeholder: kit banners with text | P2 |
| Draft card frame 512 × 768 and "locked in" stamp | 1v1 draft | placeholder: kit card | P2 |
| "NEW ITEM" ribbon 1024 × 256 (9-slice) with a sparkle behind the icon | Item reward popup | placeholder: kit panel | P2 |
| Menu background: painted harbour, 1920 × 1080 and 1080 × 1920, logo-safe top third | Behind the menu hub | partial (menu panel art only) | P2 |
| Raid shield dome (one mesh ≤ 200 triangles) and shield icon 256 × 256 | A home island after a raid (4 h shield) | placeholder: the kit's shield glyph | P2 |

## 7. App and store assets (8 rows)

The app icon (logo, all Android sizes) and the poster splash are done. For a store release:

| Asset | What it is for | Status | Pri |
| --- | --- | --- | --- |
| Adaptive app icon as two layers: foreground 432 × 432 with the logo inside the central 66%, background 432 × 432 | Android launchers that mask icons (today the whole logo is the background layer, zoomed out) | needs fix | P1 |
| Store icon 512 × 512 PNG | Google Play and the Solana dApp Store listing | missing | P1 |
| Google Play feature graphic 1024 × 500, no transparency | Play Store listing | missing | P1 |
| Solana dApp Store banner 1200 × 600 (and an optional 1200 × 1200 feature graphic) | dApp Store listing (check the store's current spec when submitting) | missing | P1 |
| Store screenshots, 1080 × 1920 or 1920 × 1080, 4-8 per store, plus one 1920 × 1080 key art | Both store listings | missing | P1 |
| Loading card 1920 × 1080 and 1080 × 1920, 10% safe area, logo ≤ 25% of the area | App and web loading screen | missing | P2 |
| Web page art: favicon (32 × 32 and 180 × 180), page background behind the game | Desktop WebGL build | missing (template thumbnail only) | P2 |
| Share-card frame 1200 × 630 PNG, transparent centre | Run result sharing | missing | P2 |

## 8. Fixes to art already in the game (9 rows)

| Asset | Problem | Fix wanted | Pri |
| --- | --- | --- | --- |
| Elite Kraken Spawn (`Kraken_Crown`, material `Kraken.mat`) | Emission is full white over the emissive map: the kraken looks washed out and pale in game | Lower the emission colour (or darken the emissive map) so the body keeps its colour | P0 |
| Cutthroat Skiff prefab | Its wake trail had no material (we set one in the game copy) | Add the trail material in your source so the next delivery does not drop it | P1 |
| Ship and monkey textures | 4096 px: Boarding Barge, Drowned Harrier, Drowned Pursuer, Gunner Cog, Needle Sloop, Powder Barge, Ram Raider, Shield Frigate, Monkey Buccaneer. 2048 px: Bomb Ketch, Net Cutter, Repair Tender and both crowned elites (5 maps each). Web builds cap at 1024 | Re-export enemies at 512, hero pieces at 1024, power of two | P1 |
| Rowboat widths (owner's note) | The rowboat-type ships do not match the width of the hulls they replace (we scale each model to the old model's footprint) | Owner to point at the boats; re-model to the target width | P1 |
| Ship pivots | Seven ships sat with the deck at or under the water; we lift them 0.1-0.2 m in game (Cutthroat Skiff, Deckhand Dinghy, Gunner Cog, Needle Sloop, Net Cutter, Ram Raider, Shield Frigate) | Pivot at the waterline centre in your source | P1 |
| Your 13 new ships and 2 crowned elites | Triangle counts not measured yet | Tell us the counts; keep to 300-1,200 (ordinary, specialist) and 1,500-2,500 (elites) | P1 |
| Showcase prefabs | They carried old game scripts (we removed them on import) | Deliver prefabs without scripts | P1 |
| Captain sails (6) | 1080 × 1080, not a power of two | Re-export at 512 or 1024 to the sail canvas spec | P2 |
| Fish bell model | 8,650 triangles, if it becomes the Siren Bell's bell | ≤ 400 triangles | P1 |

## 9. Art that exists but is not wired yet (on us, not the art team; 10 rows)

| Art (your delivery) | Goes on | What we do | Waiting for |
| --- | --- | --- | --- |
| Icons Devil's Match, Hammerhead Helm, Queen Anne's Fuse, Storm in a Bottle | the four new items | move from the Unmapped folder and wire by id | the items coming back now |
| Icon Chaos | the Chaos Logbook | same | the Logbook coming back now |
| Maelstrom Charge pack (whirlpool shader, meshes, textures) | Maelstrom Charge whirlpool | wire as the weapon's area effect | owner's mapping (TODO-FOR-YOU) |
| Mortar Barrage pack and Mortar explosion | Mortar shell, landing marker and blast | wire | owner's mapping |
| Tidal Aura effect | Siren Bell pulse | wire | owner's mapping |
| Ink Pot slick disc, ink gush and droplets | Squid Ink Pots slick and landing splash (today violet code rings) | wire with the weapon's return | Squid Ink Pots coming back now |
| Older weapon prefabs (Broadside Battery, Deck Cannons, Flamethrower, Flying Cutlasses, Harpoon Gun, Grapeshot) | their weapons | wire or drop | owner's decision |
| Parked weapon effects work (prefab library, per-weapon effects) | all weapons | re-apply the parked patch | V5 phase M9h |
| Evolution, powerup, affix and status icons, once delivered | cards and HUD | add the catalogue slots (the game has none for these yet) | your icons |
| Ramming Prow icon | nothing (the weapon was removed) | none | - |
