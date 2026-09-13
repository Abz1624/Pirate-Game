# Rogue Pirates — Crews, Community Events and Ship Inventory

13 September 2026 • GDD extension

The owner has requested Crews, daily/weekly community rankings and events, rewards for top players in a community, collaboration ship variants, and an inventory. Players cannot change Crews while their current Crew's event is active; once it ends they can join another Crew immediately. Rewards are flexible, including skins, game resources and SOL/cash, and are distributed manually/externally. These are approved product requirements. Numerical limits, event snapshot policy and aggregation below remain proposed defaults. This document extends [the GDD](Rogue_Pirates_GDD_v3_1.md) and [economy specification](Rogue_Pirates_Economy_Systems_v3_1.md).

## 1. Purpose and boundaries

Crews give players a persistent group identity. Community campaigns let partners bring their members into scheduled competitions with visible rewards and themed ships. Reuse verified ordinary Voyage scores; do not require a new combat mode or a fresh map/content pack per collaboration.

Keep the existing global weekly best-single-run kill board across every biome, tier and account progression. Add daily and weekly community/event views and Crew standings alongside it. They do not divide the global board into progression brackets. Raids, home challenges, self-tests and invalidated runs remain ineligible.

Events have free entry in the proposed first release. No skin purchase, token holding or wallet connection is required to participate in gameplay. Reward funding and any real-money distribution are separate from event scoring.

## 2. Crews and partner communities

### Persistent Crew

An account has at most one current Crew. Membership is locked for the duration of that Crew's active event. Outside that window, switching has no fee or additional cooldown. Retain one leader, up to three officers and a 2–5-character tag as proposed defaults. Crew identity includes name, emblem, description, membership policy and optional verified partner affiliation. Replace the previous universal 30-member cap with a configurable capacity: ordinary Crew default 30, partner Crew capacity provisioned for the community before invitations go live. Pagination and load testing must support the approved partner size.

- Leader: appoint officers, configure membership, transfer leadership, remove members and request disbanding.
- Officers: accept applications, invite and remove ordinary members. They cannot change ownership or event scores/rewards.
- Members: represent the Crew, view standings, share a join code and leave when the event membership lock permits.
- Joining supports open, application and invite-only modes. A share link opens a preview rather than silently changing membership.
- No Crew damage bonus, shared treasury, donations, war mode or live chat is required initially. Start with a moderated description/announcement and standings.
- Names, emblems and announcements need reporting/moderation. Event and reward records use immutable IDs, not editable tags.
- Leadership transfer is required before a leader voluntarily leaves. Disbanding does not delete completed event history or pending participant awards.

### Large community participation

The Crew is the community representation; there is no separately selected Event Tag. A player joins a sponsor's Crew for its event, then can join another sponsor's Crew for another event. The event publishes eligible Crew IDs and joining a Crew alone does not silently enroll the player in a prize event.

Proposed event snapshot: on the first eligible run after explicit enrollment, record the current eligible Crew for that event. This score snapshot is separate from the membership lock, which applies to all current Crew members while the Crew's event is active, even before they enroll or submit a run. After the event ends, later Crew changes cannot move its recorded scores or rewards.

### Event membership lock

- Lock existing members at the event's published start. Players joining an active-event Crew are locked immediately until that event ends; the join preview must show the exact unlock time.
- Unlock at the event's published competition end, not when late runs finish, standings are verified or manual rewards are delivered. Old runs still settle against their original snapshots.
- Daily resets within a multi-day event do not unlock membership. The event end governs the lock; ordinary daily/weekly leaderboard refreshes alone never lock a Crew.
- **Owner-approved: Leave when event ends.** A member can queue or cancel departure before the event end. At that server timestamp, process event closure/unlock, queued departures, then the next event's roster lock in that order, atomically for the affected membership. Pending review/rewards do not delay departure. Accept the command only once per membership/event and show its queued state. A new command received at/after the boundary is evaluated against current membership, not backdated.
- Proposed leadership fallback for queued exits: leader may nominate a willing remaining successor; otherwise promote the longest-serving remaining officer, then member, with account ID as tie-break. If everyone leaves, archive the empty Crew. This prevents a missing leader from defeating the approved exit promise. Preserve pending prizes and moderation records.
- Proposed scheduling rule: one active membership-locking event per Crew, with daily/weekly prize periods inside it. Scheduled future events do not extend the current lock. Show their start times so members can leave between events. Reject overlapping locking events in the first version; do not let administrators silently extend a published lock.
- Proposed anti-bypass rule: ordinary kicks and disbanding are deferred until unlock. Safety moderation may remove/suspend an account immediately, but cannot reassign its scores or allow another Crew enrollment before the original unlock time. Keep any such restriction in an account-level event-lock record.
- Canceling an event releases its membership lock immediately and preserves the event's cancellation/reward audit trail.
- These scheduling and moderation details are proposed edge-case defaults; the owner's confirmed rule is no Crew switching during its active event, with immediate freedom to switch after it ends.

## 3. Rankings

### Player score

For each eligible account and period, use its highest verified kill count from a single ordinary Voyage, counting every stage and survival after the final boss. Daily scores are not summed to create weekly scores. A run can contribute to the global week, its Crew views, and an enrolled partner event when it meets each published rule.

Views:

- Global weekly: existing open competition.
- My Crew: daily and weekly player standings.
- Partner event: daily and weekly player standings within the selected community; this is the primary top-player reward list.
- Crew standings: daily and weekly group comparison, optionally filtered to a partner campaign.

Proposed group score: sum the best individual scores of the Crew's top 10 eligible members for that period. Each player contributes once. Fewer than 10 contributors means remaining slots contribute zero; larger groups gain selection depth but cannot sum unlimited player scores. Display contributing members and scores. This is not perfectly size-neutral; test it rather than call it a fair normalization. Group ranking does not automatically grant extra rewards unless the event advertises them.

Community-versus-community standings, if used, follow the same published top-N formula rather than total membership or total attempts. The first event may omit that comparison and reward only individuals within each community.

### Membership and attribution

- When membership changes are permitted, joining has immediate eligibility for new runs and the new Crew's event, subject to that event's entry rules; no additional contribution cooldown is imposed.
- Freeze Crew ID, event enrollment and eligibility at run start. Joining after starting a spectacular run cannot retroactively assign it to a community.
- For a reward-bearing campaign, lock representation on the first eligible run, not its final score. Lock applies for the campaign's published duration, including its daily and weekly periods.
- Existing credited scores remain with their original roster. Leaving, being kicked or renaming the Crew cannot move scores or confiscate an already earned award.
- A player cannot leave or join another Crew during the current Crew's event. Show the lock reason and exact end time. Once the event ends, another Crew can be joined immediately while old scores and pending awards retain their original attribution.
- Keep historical affiliation for raid anti-collusion checks; leaving a Crew does not immediately make former members valid reward-farming opponents.
- Crew leaders cannot select winners manually, alter scores or exclude an eligible winner after the fact. Staff corrections require a recorded reason.

## 4. Schedule, ties and settlement

Proposed common clock: daily periods run 00:00–00:00 UTC; weekly periods start Monday 00:00 UTC. Show local time and an exact countdown. Campaigns define their start/end in advance and state whether partial opening days/weeks carry prizes.

Assign a run to the period containing its start time. Score is posted only after verified death. Preserve the existing maximum 24-hour late settlement after a period closes, subject to the stricter active-run interruption rules. Thus a daily result can remain provisional during the following day; show this explicitly. Starting a run before the reset does not submit its score to the next period as well.

Equal kills share rank; timestamp/run ID order is display-only. Publish tie handling before enrollment:

- For divisible fixed reward pools, combine the prizes for the tied occupied places and split evenly; deterministic smallest-unit remainder handling is recorded.
- For cosmetic awards, recommended default is the same award for everyone tied across the cutoff. Disclose that issuance can exceed the nominal number of winners.
- If a partner demands a strictly limited number of copies, settle the tie rule before publishing. Do not silently choose arbitrary winners or promise a fixed mint count under the all-ties rule.

Event lifecycle: draft, scheduled, active, awaiting late scores, review, finalized, rewarded, archived; cancellation is a separate recorded outcome. Partner branding, eligibility, reward schedule and scoring version lock at publication. Material changes require an announced new version or cancellation, not retroactive edits.

Daily and weekly awards can both be won unless the published campaign explicitly says otherwise. Finalization creates an operator-reviewed winner/reward manifest rather than triggering automatic payouts. Track pending, approved, delivered, failed and disputed states. In-game skins/resources are granted by an authorized operator through idempotent receipts; external rewards are paid outside the game and recorded with a reference. A manual process must still prevent paying the same allocation twice. Show reward history and any published delivery/claim requirements.

## 5. Event rewards and administration

Campaign setup specifies partner ID, visible branding, entry dates, affiliation rules, eligible runs, daily/weekly windows, top-N contributors if used, prize places, exact assets/amounts, tie treatment, late-score cutoff and disqualification/appeal process.

Recommended initial rewards: themed ship variants, sails, flags, titles and modest Gold/Rum. Event currency grants obey existing storage/overflow rules and display any overflow; do not introduce an unlimited protected currency mailbox. Skin grants are independent of treasury capacity.

Daily prizes should be affordable to repeat; a common colorway or known modest reward works better than a newly modeled ship every day. Weekly prestige can use a distinct sail, trim or emblem. Optional participation goals may award a basic variant, while leaderboard placement earns a visibly distinct edition. Neither is stronger in combat.

The owner explicitly supports flexible rewards, including SOL/cash, distributed manually/externally. Each event specifies reward description, amount/unit where fixed, distributor, funding responsibility, delivery window and required recipient details before enrollment. Export verified winners using stable account IDs and an allocation ID; collect any payout address privately and confirm it belongs to the intended recipient. No integrated wallet payout or automated cash transfer is required for initial implementation. External/manual fulfillment does not remove the need to review the actual event's distribution and platform requirements before advertising it.

Partner self-service publishing and automatic external payouts are not needed for the first release. An internal operator can configure a campaign, preview its prize budget, approve supplied art, schedule it, review verified results and grant rewards. Partners see results but do not receive unrestricted player account data.

Anti-abuse needs verified runs, duplicate-grant protection, one account contribution per roster, suspicious-account review and a published correction process. Shared IP alone is not proof of cheating. Define multi-account eligibility explicitly before attaching scarce prizes. An invalidated score promotes that player's next verified eligible run and recomputes standings before payout.

## 6. Ship skin variations

Ship skins change appearance, not stats, collision, navigation, combat silhouette readability, Weapon behavior or visibility to enemies. A skin cannot hide warnings, imitate loot or make the vessel materially harder to see. Test each on all three biomes and low visual settings.

Recommended first art system:

- One base ship rig/collider with compatible cosmetic geometry and attachment sockets.
- Hull appearance, sail, flag and emblem slots, plus a whole-ship preset that populates compatible slots.
- Partner packs assembled from these reusable pieces. Start with texture/trim/sail changes; new hull geometry is a separately budgeted premium production task.
- Preview rotation, thumbnails, in-run scale check and mobile/browser quality versions.
- Name and partner attribution, source campaign, visual rarity/edition and acquisition history. Visual rarity never changes combat power.

Per initial collaboration, budget one approved emblem, one standard palette, one winner palette, one sail/flag set, inventory/shop thumbnails and event banner. Reuse the base ship and animations. All names and logos need usage permission, including rules for continued display and trading after the collaboration ends.

## 7. Cosmetic inventory

Provide a dedicated **Ship & Cosmetics** view with owned/all filters, partner collection, slot, source, equipped state and trading status. Include preview, equip, unequip, favorite, compatible-slot indication and a transaction/acquisition history.

Unlike aggregated Item upgrade holdings, cosmetic copies are discrete instances. Two copies can be grouped visually in the inventory, but each has a unique asset ID. They do not merge into cosmetic levels. An account can equip one copy and list a spare.

Keep the owner's tradeable-cosmetic objective: awarded or purchased ship cosmetics are intended to be transferable under the supported marketplace model. Trading availability still depends on the distribution/payment decisions; do not silently replace them with account-bound items. Crew/event affiliation and a profile achievement are records, not transferable membership rights.

Changing or leaving a Crew does not remove an owned collaboration skin. Buying a winner skin does not transfer the original player's achievement badge or claim that the buyer won. Asset provenance can identify its edition without misrepresenting current-owner performance.

Use a default starter appearance that is always available. Proposed listing rule: listing an equipped cosmetic first unequips it and shows a preview of the fallback; listed instances cannot be equipped. Canceling restores availability, not automatic equipment. A sold instance leaves the seller inventory once settlement succeeds.

Freeze the equipped appearance at run start. For the first version, active-run cosmetic instances cannot be listed/transferred until that run ends or expires; unrelated cosmetics remain tradeable. This prevents one unique instance being used by seller and buyer simultaneously. UI must explain that changing the menu loadout affects the next run.

Inventory has no arbitrary purchased slot limit in the initial design; use pagination/filtering rather than deleting rewards when many copies exist. Pending/failed grants, full cosmetic asset catalog loads, incompatible pieces, revoked fraudulent grants and marketplace unavailability need explicit UI states. Asset reversal behavior must follow the eventual published transaction policy.

## 8. Data and UI additions

Logical records: Crew, MembershipHistory, PartnerCommunity, CampaignEnrollment, EventDefinition, PeriodDefinition, VerifiedScoreReference, FinalStandingsVersion, RewardAllocation, CosmeticDefinition, CosmeticInstance and ShipLoadout. Reuse RunSnapshot and RewardReceipt; do not copy run scores into editable partner spreadsheets as the authoritative source.

Freeze run affiliation/rules; derive best scores; finalize standings version; allocate rewards with a unique key per event/period/account/prize; apply each grant at most once. Concurrent daily and weekly wins are different allocations, not duplicate grants.

Screens/assets: Crew search/create/profile/roster/applications; join-link preview; event hub/detail/rules; daily/weekly tabs and period history; personal rank and top contributors; provisional/final labels; affiliation lock warning; skin collection/loadout/preview; reward history; partner banners/emblems; admin preview/finalize/grant/retry controls.

## 9. Delivery and acceptance

First combat slice is unchanged. Add Crews, verified daily/weekly views, one internally configured partner event and reusable skin inventory to the planned release workstream. They are no longer merely optional future concepts. Full partner self-service, live chat, Crew wars and co-op remain deferred.

Before launch verify:

- Same run can correctly count once in its global week and each eligible event period; daily totals never become weekly score.
- Best-run replacement, tied cutoffs, late death, pause expiry, empty roster and fewer-than-N contributors produce deterministic standings.
- Join/leave/kick/name changes cannot steal or duplicate affiliation, scores or rewards. Verify pre-start switching, locking at start, joining mid-event, daily reset without unlock, exact event-end unlock, cancellation, kick/disband bypass prevention and late settlement after joining a new Crew.
- Duplicate callbacks and payout retries do not duplicate skins or currency; daily and weekly grants remain distinct.
- A sold skin removes seller access, preserves buyer ownership, and does not transfer a winner achievement.
- Skins preserve collision, readable attacks and equivalent performance across supported browser/mobile targets.
- Event page states exact rewards, funding status where relevant, time zone, deadlines, eligibility and provisional results before a player enrolls.
