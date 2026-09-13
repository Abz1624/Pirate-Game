# Rogue Pirates — Narrative, Retention, Revenue and Production

Design discussion addendum • 13 September 2026

Status: recommendations for owner review, not newly approved canon or tested balance. Read alongside [the current GDD](Rogue_Pirates_GDD_v3_1.md), [combat content](Rogue_Pirates_Combat_Content_v3_1.md), and [economy systems](Rogue_Pirates_Economy_Systems_v3_1.md). This addendum does not replace their accepted gameplay commitments. No implementation or repository review was performed for this proposal.

Owner clarification: browser and mobile launch together. The audience is roguelite and Clash-style base-building players; age range is not yet decided. Subsequent decisions specify Unity 3D, Android/iOS native, desktop/mobile browsers and target distribution through Steam, CrazyGames and Poki. See the technical design for channel-specific restrictions and fallbacks. Do not assume a children's audience from the comic tone.

## 1. A comic world with a small story

Recommendation: add a recurring cast and a world premise, with a few milestone scenes. Avoid a campaign that needs constant chapters, cinematics, or dialogue trees. The story should explain what players already do and supply reusable situations for marketing.

### Proposed premise: The Sea Keeps Receipts

An enormous treasure was stolen from the bottom of the sea. Unfortunately, it belonged to the sea. Now an impossible tide rearranges the archipelago, sinks greedy captains, and washes them back ashore to try again. Every captain believes they can become rich enough to retire. Nobody has successfully retired.

The player starts with a terrible ship and an optimistic quartermaster. They defeat local sea tyrants, claim a home, expand into increasingly dangerous waters, and compete with other equally unreasonable captains.

This provides context for repeat deaths without reversing their mechanical consequences: the captain returns, the temporary build is lost, and only the rewards permitted by the game rules remain. Narrative revival is not an extra life during a run.

### Five world rules

1. The captain survives the fiction of sinking; the run still ends at death.
2. A claimed home is anchored and remains recognizable to its owner.
3. The hostile tide rearranges approaches to rival Territories. In gameplay, these are the section permutations already designed, not additional simultaneous maps or a new simulation.
4. A Treasury Beacon binds a guardian to the owner's loot. Defeating it secures the permitted theft once; later survival is the captain refusing to stop looting.
5. Treasures are desirable possessions, not a promise of real-world earnings. SOL and checkout remain outside character dialogue.

### Small recurring cast

- **The Captain:** player identity, primarily visual comedy and customizable appearance. Avoid requiring a fully voiced personality that conflicts with player customization.
- **Mags, the Quartermaster:** practical, dry, maintains the Ledger and introduces contracts. Wants a sensible budget; works for a pirate.
- **Crumb, the gull:** compulsive collector, steals shiny things, silently reacts to disasters. Useful in short animation without localization-heavy dialogue.
- **Existing bosses:** recurring antagonists rather than additional NPC production. Barnacle King considers the player's home his property; the ghost admiral is obsessed with procedure; the Kraken treats entire fleets as minor inconveniences.

Names and characterization are placeholders requiring owner/art review. Mags and Crumb can initially exist as portraits, silhouettes, and text; no separate navigable hub is needed.

### Minimal story delivery

- Opening: three illustrated panels, approximately 20 seconds, skippable and replayable.
- First claim: short Mags exchange after the death/results sequence introduces ownership. Do not interrupt post-boss survival or add extraction.
- Each Zone: one introduction and one first-conquest aftermath, attached to existing milestones.
- Contracts, bosses, Items and results: short optional flavor lines. Keep objectives and damage explanations separate and unambiguous.
- Repeat runs: occasional barks with cooldowns, not mandatory repeated conversations.
- Story completion never gates ordinary replay, payment, or Item reacquisition.

An initial writing scope could be one premise sheet, five character sheets, roughly eight brief milestone beats, 30 reusable barks, and one flavor line per existing Item. These are production targets, not extra gameplay systems.

### A repeatable marketing format

Use a recognizable setup, escalation, and visual payoff. The same ship, bosses, home decorations and gull should appear in game and promotions. Begin with animatics or motion comics; commission longer films only after characters and jokes work with viewers.

Possible 15–30 second episodes:

- **One More Chest:** the portal is open. The Captain sees a chest, then another. Mags holds a stopwatch. Cut to the gull delivering the Captain's hat to shore.
- **Home Security:** a proud captain unveils a terrifying guardian. The guardian immediately demands a lunch break.
- **Perfect Build:** lightning chains spectacularly across a fleet. Next attempt: the Captain proudly equips a plank. Crumb applauds once.
- **Interior Decoration:** a raider pauses to admire an extravagant ornament while the owner watches the treasury alarm.
- **New Neighbors:** an expansion reveals the Kraken. The Captain slowly turns a “WELCOME” sign around.
- **The Receipt:** Mags totals a magnificent haul, then reveals the repair bill. Use as a joke, not an unannounced gameplay repair tax.

Never depict unavailable combat, rewards, or purchases as playable footage. Label purely cinematic storytelling appropriately. Supercell's announcement describes its existing shorts and Clash-A-Rama as part of its storytelling history; that supports this format as an inspiration, not a prediction of our marketing return. [Supercell](https://supercell.com/en/news/clash-netflix/)

## 2. Quests and retention

The game already has unlock quests, targeted reacquisition contracts, mastery, upgrades and a weekly kill board. Expand those through one **Contract Board**, rather than adding several parallel task menus and currencies.

### Four purposes, one interface

- **Captain's Journey:** permanent onboarding milestones. Claim home, finish a Ledger upgrade, take a portal, arrange a defense, complete a self-test. Grants modest resources or an existing unlock once. Completion records survive Item sales.
- **Item contracts:** existing repeatable targeted reacquisition. One selected target, explicit reward and requirements, achievable with starter equipment and the player's currently accessible modes. Preserve the existing proposed two-boss/300-kill baseline for testing rather than add a second acquisition schedule.
- **Rotating bounties:** optional reasons to vary play. Examples: defeat a specified eligible enemy family, clear optional encounters, or survive overtime. Refresh offers without deleting accepted progress. Use a small bank of offers so missing a day does not erase value.
- **Mastery:** permanent optional constraints such as defeating a boss with fewer Weapon slots. Reward titles, display trophies or a limited first-completion reward; never require intentional bad builds for the main progression route.

“Bounty” currently also names a reputation measure in the economy draft. Use **Notoriety** for reputation and **Bounties** for tasks if the owner agrees. This is a proposed terminology change, not a second progression currency.

### Contract rules to define in data

Each contract needs an ID, eligible modes, access prerequisites, tracked event, target count, reward, repeatability, expiry rule, and text. Track verified events and grant once through the existing reward-receipt system.

- Show progress on death/results; a failed meaningful attempt can advance kill/exploration tasks.
- Do not require owning an Item to reacquire that same Item.
- Do not require a successful paid purchase, incoming attack, or arranged friendly raid.
- Exclude boss summons and other zero-credit spawns from kill tasks, matching the score rules.
- Preview rewards and eligibility before selection. Warn if changing a selected targeted contract abandons its progress; choose and document that behavior before UI implementation.
- Avoid speedrun tasks that accidentally punish the accepted post-boss survival loop. Measure the boss milestone, not total time until death.
- Difficulty/access should scale by what is unlocked, not silently increase because the player spent money.

### Weekly structure without weekly content production

Keep the one shared best-run Voyage leaderboard. Offer optional personal weekly goals alongside it, achievable over several runs, with ordinary resources or cosmetic progress. Do not introduce additional kill leaderboards or compulsory daily streaks.

Home challenges can reuse authored encounters with a few meaningful modifiers. Start with three tested modifiers rather than many superficial variants. Keep special modifier challenges out of the ordinary Voyage board unless those rules intentionally become available to every ordinary Voyage.

The main return reasons should remain: try another build, beat a personal best, finish a desired holding, revisit the decorated home, and improve a defense. A task layer that makes players ignore interesting drops has worked against the core design.

### Retention measurements

Observe first-session completion, retry rate after weak builds, day-1/day-7 return, runs per returning player, contract abandonment, first-week progression, and voluntary replay after rewards are exhausted. Segment by access tier and acquisition source. No invented benchmark is a launch guarantee. Pair metrics with short interviews: did players return because they wanted another run, or because they feared losing something?

## 3. Free-to-play revenue proposal

Recommend a small catalogue of known cosmetic purchases first, with marketplace fees as secondary revenue. Do not depend on trading volume to pay the game's operating costs. Cosmetic demand also requires players to actually see each other's homes and ships.

### Candidate products

- **Direct cosmetic bundles:** known sails, flags, ship appearance and Territory ornament themes. Show each component, previews, ownership and trading eligibility. Respect existing combat-readability restrictions.
- **One-time supporter pack:** a clearly specified cosmetic set and profile recognition. No automatic superior Weapon, guaranteed build, additional leaderboard lives, or exclusive combat mechanics.
- **Later cosmetic collection track:** only after a sustainable art cadence exists. If paid, make the selected track remain completable rather than selling progress that disappears at a deadline. No need for a pass in the first prototype.
- **Marketplace fee:** retain the provisional 3% only as an economic test assumption. Real fee, network costs, refunds and payment routing remain open. Earned assets being tradeable does not guarantee liquid markets.
- **Upgrade acceleration:** possible later, but higher risk. It increases access to strength, production and tradeable supply even if each individual Item rank is small. Do not use frustrating default timers to manufacture demand. Measure the effect on raids and the shared leaderboard before selling it.

No new premium currency is needed merely to obscure prices. This recommendation preserves the two gameplay currencies and payment rails already discussed.

### Trading implications of IAPs

The owner's preference for tradeable cosmetics remains. Define whether each paid product creates a tradeable asset or grants an account service; display that distinction before purchase. Paid assets transferred to another player require an explicit chargeback/refund policy so a later reversal cannot silently confiscate an innocent buyer's unrelated property.

A cash store selling an unlimited cosmetic also influences its resale demand. Earned cosmetics and paid designs need coherent supply/catalogue rules. Avoid promising scarcity and then changing issuance without disclosure. Paid purchase, trading, quest grants and duplicate ownership must all have defined outcomes.

### Advertising

Do not add ads inside combat, during boss defeat, or between portal stages. Do not sell a revive: death is the ending the owner deliberately chose. An ad after every death would also put friction precisely where we want an immediate retry.

There is a specific AdMob constraint: rewards must be non-transferable; its definition excludes rewards usable to purchase items transferable to another person. Because Gold/Rum buy tradeable cosmetics, rewarding them for an ad would conflict with this requirement. Tradeable Item progress has the same issue. [AdMob reward policy](https://support.google.com/admob/answer/7313578?hl=en-GB)

Recommendation: leave rewarded ads out of the initial economy. If ads are important commercially, test an optional non-transferable service such as a temporary cosmetic preview/photo effect, subject to provider review. That would be a service entitlement, not a tradeable collectible. Its appeal may be too weak to justify an ad; test rather than assume. Do not secretly make the currently agreed tradeable collection account-bound to accommodate ads.

Forced interstitials and “remove ads” purchases are possible business choices, but are not recommended defaults for the fast retry experience. Decide whether an ad-supported audience is essential before funding the integration.

### Platform and distribution checks

Platform is a design dependency. Google Play generally requires its billing for in-app digital goods, with specified exceptions and regional programs; peer-to-peer payments are treated separately. Do not assume one SOL checkout can serve every transaction. [Play payments policy](https://support.google.com/googleplay/android-developer/answer/9858738?hl=en)

If assets are tokenized, Play also requires relevant declarations and restricts accepting anything of monetary value for a chance at an NFT of unknown value. This makes paid random tradeable chests a particularly unsuitable default. The current gameplay-earned random chest loop needs a transaction-specific review once tokenization and distribution are selected; it is not automatically approved by avoiding a paid chest button. [Play blockchain policy](https://support.google.com/googleplay/android-developer/answer/13607354?hl=en)

If native iOS is included, Apple's current section 3.1.1 says NFT ownership must not unlock app features or functionality. That is a material conflict to resolve if tradeable Item holdings are implemented as NFTs that control run eligibility or ranks. Moving checkout to the website does not establish compliance. Apple's multiplatform purchase and regional linking rules also need transaction-specific review. [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)

These checks were made on 13 September 2026. They are not platform or jurisdiction clearance. Final reviews remain conditional on actual launch destinations, audience, asset structure and payment providers. Keep the owner's trading objective visible while evaluating compatible distribution; do not silently remove trading or assume approval.

For the simultaneous browser/mobile launch, specify shared account recovery, cross-device progression, one active run, purchase ownership, and platform-specific checkout/error flows. Test touch and desktop input from the first slice, including browser backgrounding and mobile app suspension. Share gameplay rules while allowing different presentation and payment adapters.

### Commercial validation

Model net IAP proceeds after store/payment charges, refunds and taxes; marketplace fee proceeds separately; and optional ad proceeds only if an eligible product exists. Compare with hosting, transaction, support, moderation, content and acquisition costs. Do not count player-to-player sale volume as studio revenue. Test willingness to pay for actual cosmetic previews before committing to a large catalogue or recurring content obligation.

## 4. Non-code production inventory

This is a scope checklist, not a requirement to finish every asset before prototyping. Use placeholders for the first slice. Every final asset needs an owner, source file, export format, dimensions/budget, naming/version, license, integration notes, and acceptance check.

### Visual direction and environment

- Art bible: ship and enemy silhouettes, material palette, water style, biome distinctions, camera scale, lighting, readability, color accessibility and VFX limits.
- One readable starter ship, damage states, attachment locations and cosmetic attachment standards. Additional appearance sets only after the base silhouette works.
- Modular water, shore, reefs, wrecks, ruins, connector transitions, outer boundaries, collision proxies and navigation masks.
- Terrain section layouts: the GDD proposes four families with three variants. Distinguish reusable layout templates from biome art kits; do not commission every combination as a unique bespoke map.
- Three biome kits and expansion-ring transitions; raid permutation seams and rotation-safe decoration sockets.
- Treasury Beacon idle/discovered/activating/active/defeated states; portal locked/open/transition states; chests closed/opened; eight POI types and consumed states (four in the first slice).
- Initial ornament, flag and sail sets, thumbnails and shop previews. Decorations remain non-obstructive and recognizable after section rotation.
- Backgrounds and framing for home viewing, results and marketing capture.

### Combat models, rigs and animation

- Megabonk polish additions: six original Captain portrait/pose sets (prototype three), four temporary pickup visuals/icons and timed elite warning treatments using existing enemy rigs. These add no on-foot character locomotion assets. Include the two previously specified Drowned Pursuit overtime archetypes.

- Six launch Weapon visual kits: barrels, mounts or relevant ship attachments, projectiles and impact assets. Six future Weapons remain deferred even though their designs exist.
- Seven enemy families with silhouettes, movement, attack windups, attack/recovery, hit and death states. Model variants should reuse rigs where possible.
- Spectral/infernal treatment and the additional Siren and Fire Eel assets; check these against the content manifest to avoid missing non-base enemies.
- Three bosses with phase transitions, telegraphs, attack animations, damage reactions and defeat. Boss animation requirements follow attack behavior, not an arbitrary equal animation count.
- Crumb and Mags portrait/pose sheets initially; full rigs only if justified by chosen marketing format.
- Rig/animation export conventions, root-motion policy, attachment sockets, reduced-detail versions and pooling-friendly spawn/despawn treatment.

### UI and UX

- Wireframes and interaction prototypes: onboarding; mode selection; home; Captain's Ledger; upgrade queue; storage/production; defense cards/self-test; collection; whole-holding trade preview; contracts; leaderboard; cosmetic shop; settings; support.
- Captain selection/passive preview, signature Weapon/unlock prerequisites, challenge selection/failed-versus-secured state, temporary buff duration indicators and source-based post-run build summaries. These extend the deferred wireframe TODO; they are not claimed completed screens.
- Combat HUD: Hull, XP/level, Weapon/Logbook/Item slots, run Gold, stage/timer/overtime, kills, objective state, joystick and interaction prompt.
- Minimap symbols: player, discovered Beacon, opened portal, available/consumed POIs, relevant hazards. Specify which icons are hidden until discovery.
- Level-up choices, chest reveal, slot-full handling, pause/resume, portal transition, death, secured boss objective and final reward breakdown.
- Explain temporary run Item stacks versus permanent holding progress, and run Gold versus stored Gold. Similar art without explicit labels can cause expensive misunderstandings.
- At least 48 distinct content icons for six Weapons, 14 Logbooks and 28 Items, plus currencies, ranks, rarity, status, contracts, defense cards, actions and markers. Future Weapons are extra scope.
- Complete states: loading, empty, locked, unaffordable, full, pending, success, failure, offline, expired session, stale raid quote, duplicate transaction, unavailable market and purchase restoration.
- Responsive layouts, touch targets, controller/keyboard focus if supported, safe areas, subtitles, font licenses, localization expansion and screen-reader scope where applicable.

### VFX

- All Weapon launches/impacts, explosions, lightning, flames and melee arcs, plus status feedback.
- Enemy attacks, boss danger shapes and phase transitions. Danger remains legible under a powerful player build.
- XP/resource pickups, damage/heal/crit feedback, rare-drop reveals, upgrade/merge success and cosmetic previews.
- Overtime warning, storm boundary, portal activation and safe travel readability.
- Performance tiers, reduced flash/motion settings, color-independent danger cues, and limits for overlapping effects.

### Audio

- Weapon fire, impacts, enemy identity cues, boss tells, taking damage, low Hull, healing, pickups and death.
- Beacon, portal, chest, POI, upgrade, contract, purchase and error feedback.
- Ambient water/weather and biome layers; home, ordinary combat, boss and overtime music transitions.
- Optional restrained character barks; text-only narrative must remain understandable without voice.
- Mixing priorities, simultaneous-sound limits, ducking, separate volume controls, subtitles for meaningful cues, loop points and licensed stems/source files.

### Writing, publishing and operations

- Naming glossary, tutorial copy, contract templates, Item descriptions with correct numbers, narrative beats and consistent error messages.
- Logo, app/store icon, key art, screenshots, trailer, vertical/square/landscape promotional templates and caption-safe framing.
- Character turnaround sheets, expression library, storyboard templates and approved world rules for external comic/animation work.
- Store descriptions and disclosures, privacy/consent text, purchase/refund explanations, community rules and support articles, reviewed for actual launch markets.
- Age/audience decision, localization plan, licensed fonts/music/assets, contributor rights and source-asset ownership records.
- Manual QA scenarios, device test matrix, accessibility checks, player-research scripts, analytics event dictionary and dashboard requirements.
- Customer support procedures for missing holdings, disputed trades, lost accounts, payment reversals and failed reward grants.
- Live balancing ownership, content approval/versioning, incident communication, moderation and an operational calendar. These require people and process as well as software.

## 5. What must be decided before building?

The current documents are detailed enough to begin a deliberately limited combat prototype. They are not a validated launch specification. Separate prototype decisions from public-launch dependencies so planning does not become an endless substitute for playtesting.

### Before the first playable slice

1. **Launch direction and device floor:** browser and mobile together is confirmed, with Unity 3D, Android/iOS and the specified browser/portal/Steam targets. Still define orientation, reference devices and measured performance targets. Measure the proposed enemy/projectile caps on those devices.
2. **Art/camera readability:** one ship, one enemy, one Weapon effect, one boss telegraph and one navigable section at actual play scale.
3. **Run truth:** confirm stage versus whole-run timing, boss milestone persistence, death-only ending, pause/disconnect behavior and enemy credit rules in one concise state chart.
4. **Scope:** use the existing Saltwind slice. Define what a successful playtest would show: players understand the objective, can read threats, enjoy a failed build enough to retry, and can survive briefly after a boss victory.
5. **Ownership of decisions:** identify who approves balance, art and changes to already accepted rules. Keep versioned tunables separate from fixed commitments.

### Before persistent economy and multiplayer production

1. **Authority and trust:** who verifies kills, rewards and raid outcomes; what can happen offline; save/replay validation; acceptable latency and cheating response.
2. **Raid guarantees:** entry estimate versus boss-time theft, concurrent attackers, changes in defender holdings, refund conditions, protection and expired attempts. The draft's quote/settlement approach is a proposal requiring adversarial tests, not a proven solution.
3. **Economy simulations:** play frequency, first-week affordability, repeated losses, storage saturation, late-game surplus, duplicate accumulation and trade supply. Include ordinary non-trading players, not just active marketplace users.
4. **Procedural fairness:** connectivity, boss reachability, decoration readability and comparable defense strength across permutations. Randomization must not routinely erase the value of an owner's defense choices.
5. **Progression ownership:** exact rank merge previews, listing freezes, pending grants, quest gates, refunds and accounts without wallets. The entire game must remain playable through gameplay acquisition.
6. **Retention catalogue:** approved contract templates and reward budgets; prevent rotating tasks from quietly becoming the dominant currency faucet.

### Before public monetization or launch

1. Confirm audience, countries, distribution, asset/token model, payment routing, custody/account recovery, moderation and support responsibility.
2. Approve a small product catalogue, refund/trade-reversal rules and verified price disclosures. Do not implement every revenue idea at once.
3. Validate shared-leaderboard scoring, trust, tie/week-boundary rules and acceptable paid-progression advantage. Keep the owner's single-board choice; communicate what affects opportunity.
4. Set operating budgets and monitoring: crashes, performance, grant failures, duplicate settlement, economy inflation and support backlog.
5. Complete privacy/consent, ratings, licensing and provider-specific review; run purchase/recovery tests with no real customer funds first.
6. Establish content capacity. Promise seasons, voiced episodes or frequent cosmetics only at a cadence the team can actually sustain.

### Recommended next design decisions

- Specify mobile delivery channels and age audience within the confirmed simultaneous browser/mobile plan.
- Review the proposed comic premise and cast; decide whether the tone is slapstick, spooky comedy, or a mixture.
- Agree whether the first revenue test is known cosmetic bundles, with ads deferred until a compatible offer is identified.
- Then turn the existing first-slice scope into an assigned production backlog with asset budgets and acceptance criteria. Full-launch art and marketing films can follow evidence that the core run is enjoyable.

## 6. Follow-up implementation-readiness review

Reviewed against the current main, combat, economy and Crew specifications after the event-duration Crew-lock correction. No source code was inspected. Conclusion: enough design exists to begin the first combat slice; several precise rules and technical/UX decisions remain before production systems can be implemented consistently. Additional Weapons, bosses, currencies or retention systems are not prerequisites.

Follow-up resolution: the five findings below are preserved as review history. Combat now defines eight themed POIs, a distinct Drowned Pursuit overtime fleet and same-tick resolution; economy defines pause/lifecycle clocks; Crew rules include the owner-approved queued exit. Unity/platform/save direction is selected; [technical design](Rogue_Pirates_Technical_Design.md) and [milestone backlog](Rogue_Pirates_Implementation_Backlog.md) provide the new handoff plan. Exact numerical defaults still require playtests. First-run wireframes are explicitly deferred by the owner, rather than treated as a blocker to the initial sandbox.

### Resolved specification gaps — historical review text, not current implementation instructions

- **POI count conflict:** combat section 11 allows raids six/eight POIs for two/three Zones, but offers only four types and forbids duplicate types per stage. Proposed resolution: require all four types once, then allow additional raid copies under a stated per-type cap. Exact type weighting and integer allocation of the 40% highest-Zone share need defining. Do not implement contradictory constraints.
- **Overtime scaling of existing enemies:** combat section 9 gives scaling curves but does not state whether ordinary enemies already alive receive updated Hull/damage or only new spawns do. This affects difficulty, kiting and the entity cap. Define separately for each stat; preserve the explicit boss rule that Hull does not inflate repeatedly during its fight. Keep this as a design decision, not an inferred change to current balance.
- **Same-tick resolution:** define what happens when player and boss die in one simulation tick, and when a pickup/level-up/portal interaction coincides with fatal damage. Recommended starting principle: resolve already valid combat events deterministically, secure a legitimate boss kill once, then end the run if the player remains dead; do not let a later menu choice revive them. Specify interaction cancellation and fatal-hit prevention ordering explicitly.
- **Pause and deadlines:** level-up choices pause simulation, while the 30-minute ranking restriction names manual pause only. Define whether choice-screen wall time counts, how browser backgrounding is classified, and what occurs if an actively played run exceeds the event's final late-score cutoff. A leaderboard deadline must not become an invented extraction or erase already secured rewards.
- **Event lock usability:** back-to-back events can leave no practical time to switch Crews even though switching is allowed at the boundary. Proposed solution: a leave-at-event-end action processed before the next event's roster lock, with clear UI. This detail is not yet an approved rule. Verify canceled events, leadership transfer and administrative removals against the same lock state.

### Required handoff artifacts

1. A short technical design: browser/mobile runtime, supported operating systems/delivery channels, device floor, simulation ownership, persistence and cross-device sessions. The GDD already defines logical records; implementation needs schema versions, service boundaries and failure handling.
2. Wireframes for the complete first-run journey: start, movement, choice, chest, Beacon, boss victory, continued survival, death, results and first home/upgrade. Follow with Crew join/lock/event rules and cosmetic inventory flows. An asset list alone is not an interaction specification.
3. A versioned content manifest and validation rules derived from the existing formulas: stable IDs, units, dependencies, unlocks, cap behavior and references to visual/audio assets. Keep tuning values editable rather than burying them in implementation.
4. A first-slice backlog with acceptance criteria and explicit exclusions. Start with the existing Saltwind scope, then add stage progression, persistence, raids, Crews/events and skins in dependency order. A smaller one-Weapon sandbox can validate movement/performance before building the full slice.
5. A device/performance test plan and reward-integrity test plan. Demonstrate that powerful builds remain responsive and score/reward receipts survive interruption without duplication. Public competitive runs require a chosen verification approach before they count toward prizes.

Exact damage values, upgrade prices and acquisition rates are tuning hypotheses, not reasons to delay the prototype. Public trading, payments, event prize operations and launch audience/distribution still require their own decisions before activation. Narrative and cosmetic art can be refined alongside prototype work.
