# Appendix C — Detailed Asset Checklist

**Authority note:** All referenced candidate assets remain unapproved. `references/**` is excluded; only accepted original or commercially licensed production assets may ship.

# Rogue Pirates — Asset and production checklist

Companion to the [master specification](../00_MASTER_IMPLEMENTATION_SPEC.md), [combat catalogue](A_COMBAT_CONTENT_VALUES.md), [content catalogue](../04_CONTENT_CATALOGUE.md) and [social specification](../09_SOCIAL_NARRATIVE.md).

## What we actually have

Owner instruction: **ignore every asset in `references/`**. No excluded collection is counted as available production art. The earlier reference README is not an asset approval or an accurate description of the current usable inventory. Do not reintroduce excluded assets via copies in the Unity project.

The [complete file inventory](../../Rogue_Pirates_Asset_Inventory.json) lists 1,218 selected asset-related files physically present under `Pirate Game Unity/Assets`, about 512.5 MiB. It includes 67 FBX, 45 glTF, two OBJ, 366 prefabs, 225 materials, 339 PNG, 25 standalone animation clips and 16 animation controllers, plus other image/shader/UI files. These counts overlap in purpose and include dependencies, duplicates, demos and packages; they are **not 1,218 distinct usable game assets**. None of the inventoried files were Git LFS pointer stubs at scan time.

No WAV, OGG, MP3, AIF, AIFF or FLAC files were found in that scanned tree. This does not prove that no audio exists elsewhere or inside an uninspected package. The six UXML and five USS files found are package/demo paths, not evidence of a completed game UI. One TTF font exists; its licensing/localization coverage still needs confirmation.

Audit depth: file existence, size, extension and basic glTF structure/dependency checks only. No prefab dependency walk, rig inspection, visual QA, license certification, Unity import or performance test was performed. No game code was reviewed. The JSON excludes source code, metadata sidecars, scenes and unrelated project/config files; it is an asset-source inventory, not a complete Unity dependency/export manifest.

Status vocabulary:

- **FOUND / UNVALIDATED:** relevant filename exists; inspect before assigning it.
- **NEEDED:** required deliverable with no approved match identified. This is not a claim that every possible candidate file was visually inspected.
- **EXCLUDED:** owner-excluded references, and any derivative not independently approved.
- **LATER:** outside the first slice or conditional on a product decision.
- A checked box means **accepted and integrated for its milestone**, not merely downloaded. Every box starts unchecked in this audit.

## Existing candidates to inspect

All paths below are relative links to files/folders in the workspace. Presence does not establish authorship or permission. For any candidate derived from excluded references, replace it rather than marking it approved.

- [ ] **FOUND / UNVALIDATED — starter ship:** [CutlassSloop prefab](../../Pirate%20Game%20Unity/Assets/_Game/Prefabs/Player/CutlassSloop.prefab) and [pirateship FBX](../../Pirate%20Game%20Unity/Assets/3D%20Models/Ships/Pirate_ship/pirateship.fbx). Inspect one coherent base rig/silhouette; do not equate the 45 ship glTF variants with approved collaboration inventory. Their provenance needs particular checking.
- [ ] **FOUND / UNVALIDATED — Sharks/Hammerheads:** [Shark FBX](../../Pirate%20Game%20Unity/Assets/3D%20Models/Shark/Shark/Shark.fbx), [Hammer Shark FBX](../../Pirate%20Game%20Unity/Assets/3D%20Models/Shark/Hammer_Shark.fbx), [additional HammerHead](../../Pirate%20Game%20Unity/Assets/HammerHead.fbx). Select compatible model/rig/animation; several paths may be duplicates.
- [ ] **FOUND / UNVALIDATED — Kraken-family candidate:** [Octopus FBX](../../Pirate%20Game%20Unity/Assets/3D%20Models/Octopus/Octopus.fbx). Does not establish Kraken Spawn or Infernal Kraken readiness.
- [ ] **FOUND / UNVALIDATED — enemy vessels:** [Cursed vessel meshes](../../Pirate%20Game%20Unity/Assets/Mesh/Cursed). Potential Cannonboat/ghost visual sources; confirm origin and readability.
- [ ] **FOUND / UNVALIDATED — geometry:** [existing island prefabs](../../Pirate%20Game%20Unity/Assets/_Game/Prefabs/Environment/Islands), [tropical cliff FBX](../../Pirate%20Game%20Unity/Assets/Exported%20Assets%20FBX/Cliffs_Tropical%201.fbx), tree models. Existing islands are not automatically 48-meter connector-valid sections.
- [ ] **FOUND / UNVALIDATED — water:** [Stylized Water For URP](../../Pirate%20Game%20Unity/Assets/Stylized%20Water%20For%20URP), [Simple Water Shader](../../Pirate%20Game%20Unity/Assets/IgniteCoders/Simple%20Water%20Shader), [Water prefab](../../Pirate%20Game%20Unity/Assets/Prefabs/VFX/Water.prefab). Choose one appropriate licensed treatment and benchmark it; importing multiple systems is not a requirement.
- [ ] **FOUND / UNVALIDATED — props:** [cutlass](../../Pirate%20Game%20Unity/Assets/3D%20Models/cutlass_sword/cutlass_sword.fbx), [buoy](../../Pirate%20Game%20Unity/Assets/3D%20Models/ocean_buoy/ocean_buoy.fbx), [lighthouse](../../Pirate%20Game%20Unity/Assets/3D%20Models/light_house/light_house.fbx), [coin](../../Pirate%20Game%20Unity/Assets/Mesh/Coin/Coin.fbx). Match them to actual mechanics; a lighthouse is not an extra required POI type.
- [ ] **FOUND / UNVALIDATED — effects:** [VFX prefab folder](../../Pirate%20Game%20Unity/Assets/Prefabs/VFX) contains named candidates for cannon shooting, damage, XP/Gold, barrels, flamethrower, wake, cutlass, lightning, burn trail, chest, buoy, Beacon and splash. Each needs dependency, rights, readability, pooling and low-quality checks. A prefab name does not prove the effect works.
- [ ] **FOUND / UNVALIDATED — animations:** [AnimationClip folder](../../Pirate%20Game%20Unity/Assets/AnimationClip) contains swim, attack, windup, open/close and collapse names. Map each to a verified rig; generic `Armature_*` names do not establish character coverage.
- [ ] **FOUND / UNVALIDATED — optional gull/props:** [StylizedCharacterPack](../../Pirate%20Game%20Unity/Assets/StylizedCharacterPack) includes named gull, chest, barrel, coin and crystal models. Package/demo content is not approved narrative art. No on-foot character production is needed for Captains.
- [ ] **FOUND / UNVALIDATED — font:** [LiberationSans.ttf](../../Pirate%20Game%20Unity/Assets/TextMesh%20Pro/Fonts/LiberationSans.ttf). Confirm notices, character coverage, fallback fonts and atlas settings.

## Acceptance record for every production asset

- [ ] Stable asset ID, role, milestone, owner and status.
- [ ] Original editable source and export; author/vendor, rights evidence and attribution obligations. Partner assets include permission for continued account-bound display after the event.
- [ ] Dimensions/units, orientation, pivot, attachment sockets, rig and material dependencies.
- [ ] Triangle/bone/material/texture/particle/audio budgets assigned after M0 profiling; measured imported size and memory recorded.
- [ ] Visual comparison at real gameplay scale, light/dark water backgrounds and supported aspect ratios.
- [ ] Low/high presentation variants where needed; telegraphs and collision remain equivalent.
- [ ] Import settings, GUID/reference integrity, pooling reset behavior and content-version binding validated.
- [ ] Thumbnail/icon/localization keys and required state variants supplied.

Do not fund a separate model for every Item. Most Items/Logbooks need icons and effect feedback; reusable projectile/prop assets can carry their mechanics.

## World and ship art

- [ ] **NEEDED — art bible:** silhouette scale, palette/material style, water, biome contrast, damage language, outlines, rare-reward treatment and color-independent warnings.
- [ ] **NEEDED — one approved starter ship kit:** hull, sail, flag/emblem slots, Weapon sockets, wake origin, hit/damage/sinking presentation, menu preview and fallback appearance. Candidate files above require acceptance first.
- [ ] **NEEDED — 12 reusable section templates:** open water, reef channels, wreck cove and ruined harbor; three geometry variants each. Use 48-meter cells, six-meter corridors, anchors/connectors and valid boss arenas from main §4. These are templates, not 12 new maps per biome.
- [ ] **NEEDED — Saltwind kit for slice:** water/shore/reef/wreck/harbor pieces, obstacle proxies, connector seams, spawn landmarks and Dread boundary.
- [ ] **NEEDED — Gravewater and Devil's Maw kits:** material/prop variants reusing compatible geometry; readable spectral/fire states and expansion-band transitions.
- [ ] **NEEDED — one Treasury Beacon kit:** sealed, discovered, activating/interrupted, active and defeated states. No separate functional storage buildings.
- [ ] **NEEDED — portal kit:** locked/open/transition states, minimap marker and preserved safe-route readability. An opened intermediate portal is not extraction.
- [ ] **NEEDED — chest kit:** closed/open/consumed, affordability/key-free feedback and rarity reveal. Separate in-run and permanent results-chest presentation without requiring separate rigs.
- [ ] **NEEDED — eight POI kits:** Repair Wreck, Powder Cache, Signal Buoy, Smuggler's Oath, Davy Jones' Contract, Salvage Bell, Castaway Idol, Treasure Chart. Each needs available/interaction/consumed/unavailable states as applicable; Signal Buoy adds charge and gilded state. Slice requires the first three plus Treasure Chart.
- [ ] **NEEDED — four temporary pickup kits:** Repair Supplies, Battle Signal, Tailwind, Salvage Chime; distinguish from permanent Items and XP.
- [ ] **NEEDED — pickups:** XP, run Gold, dropped Item, heal and their attraction/collection/despawn treatments; no collectible Rum pickup is implied unless its source is specified.
- [ ] **NEEDED — five basic cosmetic product families:** sail recolor, flag, decorative wreck, dock/reef ornament and trophy pedestal; known variants and instance thumbnails. Quantity beyond the first approved designs follows budget.

## Combat models, animations and VFX

For each enemy: idle/move, tell, attack, recovery, hit, death and pooled reset. Procedural motion may replace animation clips where readable; root motion must not determine simulation damage or position.

- [ ] **NEEDED — seven base families:** Shark, Cannonboat, Swordfish, Hammerhead, Manta, Crab, Kraken Spawn. Shark/vessel/octopus candidates exist; no complete accepted family kit is verified.
- [ ] **NEEDED — Gravewater/Devil's Maw family variants:** enumerate from combat §§8.2–8.3; reuse rigs/materials where appropriate while preserving attack distinctions.
- [ ] **NEEDED — Siren and Fire Eel:** their distinct tells/projectiles/attack and death states.
- [ ] **NEEDED — overtime Pursuer and Harrier:** clearly distinguish the new threat category; Harrier projectile tell and body/attack treatment must survive reduced VFX settings.
- [ ] **NEEDED — timed elite treatment:** minute-3 Hammerhead and minute-8 Kraken Spawn warning/elite visuals; reuse existing rigs instead of commissioning separate bosses.
- [ ] **NEEDED — Barnacle King:** Shell Slam, Barnacle Volley, Crab Call, Rolling Charge, phase transitions, vulnerabilities and death; summon presentation. First-slice boss.
- [ ] **NEEDED — Drowned Admiral:** Ghost Broadside, Cursed Wake, Fog Order, Crossfire, vulnerability and death; ghost summons and readable fog.
- [ ] **NEEDED — Infernal Kraken:** Tentacle Slam, Burning Rings, Drag Current, Infernal Sweep, vulnerability and death. Tentacles are emitters, not extra rewardable bosses.
- [ ] **NEEDED — Deck Cannon/Broadside:** sockets, muzzle, projectile, ordinary/crit impact, pierce and fragment feedback.
- [ ] **NEEDED — Powder Kegs:** deployment, fuse/arming, explosion radius, chained/secondary explosion distinction. Avoid physical navigation blockage.
- [ ] **NEEDED — Flying Cutlasses:** orbiting blade, hit arc and bleed feedback without terrain-through damage.
- [ ] **NEEDED — Flamethrower:** cone, ignition/burn stacks and recovery; low-overdraw mobile version.
- [ ] **NEEDED — Lightning Mast:** primary strike, chain/arc, shock and Storm in a Bottle burst; source clarity at proc cap.
- [ ] **NEEDED — shared Item effects:** ink pool/slow, healthy-target hit, near-miss burst, ram, Crown/streak stacks, Leviathan pulse and Last Rum prevention/immunity expiry. Reuse shared effect primitives.
- [ ] **NEEDED — shared combat feedback:** damage/crit, healing, low Hull, armor/vulnerability, elite identity, spawn/retire warning, victory marker and sinking.
- [ ] **NEEDED — overtime/boundary:** ten-minute warning, escalating storm, Dread damage, contraction and portal-route legibility.
- [ ] **NEEDED — quality/accessibility pass:** reduced flash/shake, overlapping effect limits, telegraph priority, danger shapes and equivalent low/high tiers.
- [ ] **LATER — six expansion Weapon kits:** Harpoon Launcher, Mortar Deck, Anchor Flail, Ghost Cannons, Chainshot, Siren Bell. Existing assets with matching names do not promote them into launch scope.

## Content icons and portraits

Need **48 distinct launch content icons** (six Weapons +14 Logbooks +28 Items), plus six Captain portraits if the full roster is retained. Reuse these icons across HUD, unlocks and collection; do not count repeated UI placements as new drawings. No matching approved icon set was established.

- [ ] Weapon icons: Deck Cannon, Broadside Battery, Powder Kegs, Flying Cutlasses, Flamethrower, Lightning Mast.
- [ ] Starter Logbook icons: Gunner, Powdermaster, Navigator, Carpenter, Lookout, Quartermaster, Scavenger, Cartographer.
- [ ] Unlockable Logbook icons: Ironclad, Fortune, Tidecaller, Admiral, Plunderer, Chaos.
- [ ] Common Item icons: Lucky Doubloon, Spare Planks, Gunner's Glove, Powder Monkey, Shark Tooth Necklace, Manta Sail, Reef Carapace, Crow's Nest.
- [ ] Uncommon Item icons: Skeleton Key, Grapeshot Pouch, Double-Fuse Cord, Serrated Scabbard, Dragon's Breath Nozzle, Copper Conductor, Broadside Drum, Hammerhead Helm.
- [ ] Rare Item icons: Smuggler's Ledger, Black Spot, Dead Man's Compass, Kraken Ink Sac, Cursed Coin.
- [ ] Epic Item icons: Storm in a Bottle, Devil's Match, Queen Anne's Fuse, Barnacle Crown, Jolly Roger.
- [ ] Legendary Item icons: Leviathan Heart, Captain's Last Rum.
- [ ] Captain portraits: Rook, Brine, Flint for slice; Vale, Cinder, Tempest after profile validation. Selected/locked portraits and passive symbols, no playable on-foot rigs.
- [ ] Shared UI symbols: Gold/Rum, XP/Hull, Weapon/Logbook capacity, ranks/rarities, statuses, four powerups, RNG tools, POIs/Beacon/portal, navigation/hazards, shield, upgrade time, pending sync, report, favorite, ad reward and paid-entitlement status.
- [ ] Defense cards: seven families, five patterns, two hazards and three boss-trait symbols, with compatible/locked state. Cosmetic rarity never implies defense power.
- [ ] Mastery/profile awards: Quick on the Draw, Single Cannon Pennant, Steady Hands, overtime milestones and global weekly participation/top-10 badges. Titles and decorations are account-bound records.

## UI/UX screens and state coverage

All are **NEEDED as validated product flows**, even if some prefabs exist. Wireframes remain a later TODO as requested; functional prototypes can precede final art. Each screen needs loading, empty, locked, error/offline and applicable pending/success states, keyboard/touch navigation and safe-area layouts.

- [ ] Entry/loading, guest/account sign-in, link conflict, recovery and channel-unavailable states.
- [ ] First conquest/tutorial prompts, start/Captain/mode/biome/tier selection and goal pinning.
- [ ] Combat HUD: Hull, XP/level, slots/Items, run Gold, kills, stage clock/overtime, objective and touch controls.
- [ ] Minimap: discovered/undiscovered objects, portal, heading, hazards and broad/exact late Beacon clues.
- [ ] Level choices, Refresh/Skip/Banish, fallback/Continue, POI choice and chest reveal.
- [ ] Pause/reason stack, reconnect, takeover, resume, paused allowance, eligibility loss and expiry warnings.
- [ ] Boss secured while play continues, stage transition, death and results showing objective success separately from death.
- [ ] Results: generated/secured/theft/overflow rewards, immutable permanent chest reveal, quest/mastery progress and source-based build/death summary.
- [ ] Home/expansion overview and temporary-versus-owned area distinction; later shared visit view if retained.
- [ ] Ledger: Treasury, production collection, capacities/exposure, protection, upgrade comparison/queue and full-storage feedback.
- [ ] Collection: licenses, quests, eligible/sealed Items, total progress/derived rank, source/destination reforge preview and active-run locks.
- [ ] Contract Board: unlock journey, targeted contract selection/change confirmation and three mastery presets. Rotating bounty UI waits for its approved catalogue.
- [ ] Defense Director: card/pattern budget, compatible trait choices, Quick/Custom setup, permutation preview and no-reward practice results.
- [ ] Raid list/quote/fee, fewer-than-three candidates, loading, boss pending/actual theft, shield race/refund and resource loss report.
- [ ] Global weekly and Crew daily/weekly standings: own rank, tied ranks, build/context, period history and provisional/verified states.
- [ ] Crew search/create/profile/roster/application/invite preview; membership lock, queued leave/cancel, succession and moderation states.
- [ ] Event hub/detail/enrollment: exact rules/prizes, dates/time zone, affiliation snapshot, complete-submission cutoff, verification pending, delivery history and appeal.
- [ ] Ship & Cosmetics: filters/pagination, compatible slots, preview/equip/fallback, discrete duplicates, provenance and next-run loadout warning.
- [ ] Resource and paid cosmetic shop, product preview, purchase result, restoration/refund state, Captain's Chronicle track, Sponsored Cache, daily/weekly event panels and Gilding flow.
- [ ] Conditional checkout/restoration/refund and channel-disabled commerce/link states; no active ads/paid revives implied.
- [ ] Settings: audio, graphics/accessibility, language, controls, support/report and account/session management.
- [ ] Internal operator UI: content/version preview, event schedule/eligibility/prizes, winner review/export, idempotent grant/retry, dispute trail and restricted access. Not a public partner builder.

## SFX, music and audio production

No approved audio identified. Budget reusable event families with a few variations rather than a unique recording for every Item or hit. Mixer groups, voice limits, pitch variation, priorities and loop transitions are implementation data supplied with the audio.

- [ ] Six Weapon families: fire/deploy/loop/recovery and relevant impacts; separate projectile and explosion/chain cues where helpful.
- [ ] Enemy families: movement/identity, attack windup, attack, hit and death; spectral/infernal variants can reuse processed sources.
- [ ] Three bosses: recognizable attack tells, phase/vulnerability, summon and defeat cues; overtime Pursuer/Harrier alerts.
- [ ] Player damage, low Hull, heal, near miss, Last Rum, sinking and objective-secured cues.
- [ ] XP/Gold/Item/powerup pickup; chest/rare reveal; Beacon/portal; eight POI interaction/consume cues, with shared layers.
- [ ] UI hover/focus/confirm/back/error, insufficient funds, pending/settled receipt, upgrade complete, unlock/contract/mastery and Crew/event reward.
- [ ] Sea/wind/shore ambient loops; biome/weather layers; non-disruptive home ambience.
- [ ] Music states: home/menu, ordinary combat, boss, overtime, death/results. Stem-based transitions may reuse one theme; do not require a full album before slice testing.
- [ ] Optional Mags/Captain/boss barks only after narrative approval; meaningful warnings always also visible without sound.
- [ ] Deliver editable source/stems, licensed exports, loop points, loudness/peak targets, compression/import settings and native/web memory checks.

## Collaboration, narrative and publishing

- [ ] Per initial partner: approved emblem, standard palette, winner palette, sail/flag set, inventory thumbnails and event banner. Reuse ship geometry/rig; new hulls require separate budget.
- [ ] Partner usage/provenance sheet: issuer/edition, allowed display/trade terms, winners versus current owners, and approval sign-off.
- [ ] Narrative approval: premise/world rules and naming glossary. Mags/Crumb designs and six Captain visual profiles; portrait/pose sheets first.
- [ ] Conditional story art: opening three panels, Zone introduction/aftermath beats, optional flavor text and reusable barks. No fully voiced campaign required.
- [ ] Marketing templates: logo/app icon, key art, store/portal capsules, screenshots, short gameplay trailer and vertical/square/landscape collaboration layouts. Obtain current per-channel export requirements when publishing.
- [ ] Optional comics/reels: expressions, turnaround/pose library, storyboard/animatic template, caption-safe areas and music rights. Reuse approved world assets; distinguish cinematic scenes from actual gameplay.
- [ ] Copy/localization: tutorial, effects with dynamic numbers, quest/rank/holding explanations, errors, support articles, store text and translated font coverage.
- [ ] Operational materials: age/market decision, community rules, account recovery/refund/entitlement dispute flows, privacy/consent/reviewed event terms, manual QA and device matrix, analytics dictionary, restore/incident and prize-delivery runbooks. Assign owners before public use.

## Production order

1. **M0/M1:** primitives or one approved ship/Shark, one section/water treatment, Cannon feedback, minimal HUD and temporary test sounds. Validate camera and mobile budget before buying a large set.
2. **M2:** Saltwind slice kits, four Weapons, seven enemy families/Barnacle/overtime, four POIs, baseline icons, three Captain portraits and first-run/results flows. Final first-run wireframes due here.
3. **M3/M4:** accounts/Ledger/collection/defense/raid UI, remaining content/biome kits, three mastery presets and approved cosmetics.
4. **M5:** Crew/event/inventory/admin interfaces and one partner variant pack; test grants with no real payouts.
5. **M6/M7:** approved commerce/channel flows, complete audio/accessibility/localization, storefront/marketing and operational documents.

Assign an artist/designer/audio owner, estimate and acceptance reviewer to these deliverables after the representative asset test. File counts cannot provide a trustworthy art budget or completion percentage.
