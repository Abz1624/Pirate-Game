# 05 — Bounty Waters (PvPvE)

Eight to twelve captains, one small map, a monster population, and eight minutes.

## The design problem

The brief: killing monsters should be low risk and low XP, killing captains should be high risk and high XP, and neither should dominate. The outcome should be decided by skill *and* randomness, with a genuine tradeoff in both directions.

The obvious version of this mode fails in a predictable way. If monsters give safe XP and captains give risky XP, the optimal play is to farm monsters in a corner until you are strong enough that fighting captains is no longer risky. Every player independently discovers this, and the mode becomes a farming simulator with occasional accidental violence.

**The fix is to make farming unsafe rather than making it unrewarding.** If the XP you bank is a *resource other people can take from you*, then sitting in a corner accumulating value makes you a target rather than making you safe. The farmer is not avoiding risk; they are storing up someone else's payday.

Five systems implement this. They are interlocking, and removing any one of them breaks the others.

## Match format

| Property | Value |
| --- | --- |
| Players | 8–12, solo queue (duos and trios as separate playlists later) |
| Duration | 8 minutes hard cap `[TUNE]` |
| Map size | Roughly one third of a Dungeon Hunt arena |
| Monster population | Continuous respawn, density `[TUNE: 60%]` of an equivalent Tide |
| Death | Respawn after 8 seconds. **Never elimination.** |
| Win condition | Highest **Bank** when the clock expires |

Everything in [03 — Core Combat](03-core-combat-and-progression.md) applies unchanged. Same hulls, same weapons, same arcs, same elements.

## 1. Plunder: XP is a droppable resource

The keystone. Everything else depends on it.

Your XP lives in a **Bank**. It drives your level-ups, and it is the win condition. It is also **droppable**.

When you sink:

- You drop a share of your Bank as floating **Plunder** orbs
- Your killer immediately absorbs **half** of what dropped
- The other half scatters on the water for `[TUNE: 20 seconds]` and anyone can collect it — including you, if you can get back in time

```
dropRate = 25% + (5% × Heat)     // 25% at Heat 0, 50% at Heat 5
```

You keep your **level and your drafted cards**. You lose banked value only. That distinction is critical: death costs you the score, never the build. A player who dies three times is still dangerous, still fun to play, and still in the match.

### Why this fixes the mode

The quiet farmer who has spent five minutes accumulating a large Bank is now the single most valuable target on the map. Their safety was an illusion the whole time. Meanwhile the hunter who kills them converts five minutes of someone else's patience into an instant payday — which is a far better feeling than five minutes of their own farming.

And because half the Plunder scatters rather than going to the killer, **the corpse is a fight**. A duel that ends in a kill immediately becomes a scramble as anyone nearby converges on the floating orbs. Third-partying is not a problem to be designed out here; it is the intended texture of the mode.

## 2. Heat

The risk-balancing meter for hunting. Every captain kill raises your **Heat** by 1 — or by 2 if the victim's Heat was higher than yours. Heat decays by 1 for every `[TUNE: 60 seconds]` without a captain kill.

| Heat | Title | Upside | Downside |
| --- | --- | --- | --- |
| 0 | — | — | — |
| 1 | Marked | — | Position pinged on the minimap every 15s |
| 2 | Wanted | +10% XP from captain kills | Ping every 10s; monsters aggro you from further away |
| 3 | Notorious | +25% XP; +1 rarity tier weight in drafts | Ping every 6s; drop rate 40% |
| 4 | Feared | +50% XP; extra reroll charge | Permanently visible on the minimap |
| 5 | **Dread Captain** | +100% XP; guaranteed Rare or better in every draft | Visible to all, drop rate 50%, and your kill is announced match-wide |

All `[TUNE]`.

### Why this is self-correcting

The player who is winning by hunting becomes progressively easier to find and progressively more valuable to kill. The mode rubber-bands **attention** rather than power — no one's damage is nerfed for doing well, they just become the obvious target. That is a much healthier correction than a stat penalty, because it is enacted by other players rather than by the system, and it is completely legible.

Reaching Dread Captain should feel like an achievement and a death sentence at the same time. The match-wide announcement is the point.

## 3. The dual-track draft

The tradeoff has to exist inside the power-up choice too, or the two playstyles converge on the same build.

Every card is stamped **Predator**, **Harvester**, or **Neutral**.

| Track | Strong against | Weak against | Example cards |
| --- | --- | --- | --- |
| **Predator** | Captains — armour pierce, burst, tracking, reveal, disengage denial | Deals reduced damage to monsters `[TUNE: −25%]` | `Hit_the_Weakest_Point`, `Expose_Weakness`, `Crippling_Shot`, `Overwatch`, `Misdirection`, `Blinding_Glare`, `Vampiric_Strike`, `Ghost_Mode` |
| **Harvester** | Monsters — wide AoE, pickup radius, sustain, clear speed | Deals reduced damage to captains `[TUNE: −25%]` | `Rain_of_Iron`, `Bombardment`, `Scatter_Shot`, `Leeching_Rain`, `Sticky_Fingers`, `Supply_Run` |
| **Neutral** | Raw stats, no penalty either way | — | `Extra_Power`, `Fleetfoot`, `Hardened_Shield` |

### Doctrine: the pool shifts with your behaviour

A hidden-to-you-but-visible-to-others meter running from −100 (pure Harvester) to +100 (pure Predator). Captain kills push it positive; monster kills push it negative, weighted by recency.

Your Doctrine biases which cards you are offered. At +60 you might see two Predator cards and one Neutral; at −60 the reverse.

This is not a lock. Neutral cards always appear, and Doctrine drifts back toward zero if your behaviour changes. But it means **the game leans into the identity you are already playing**, which makes committing feel supported rather than punished, and makes hybrid builds genuinely mediocre rather than artificially forbidden.

### Doctrine is visible on your ship

Your sails and flag change with Doctrine. Deep Predator flies blood-red; deep Harvester flies gold; neutral stays with your cosmetic theme.

This is the best single decision in the mode. An approaching ship's colours tell you, at a glance and across the map, whether it is built to hunt you or built to farm. A red sail on the horizon is a threat. A gold sail is an opportunity. Players will read this instantly and it turns every sighting into a decision.

It also means a Predator cannot ambush from a position of anonymity, which is the correct balance against how strong Predator cards are in a duel.

## 4. Randomness with agency

The brief asked for outcomes decided by skill *and* randomness. Randomness alone is a slot machine; skill alone makes the mode solvable. Three mechanisms layer control on top of variance.

### Rerolls are earned by farming

One reroll charge per `[TUNE: 25]` monster kills.

This is the mechanism that makes monsters matter to a hunter. Farming does not just give XP — it buys **draft control**, which in a build-driven game is worth more than XP. A pure Predator who never touches a monster is at the mercy of their offers; one who clears a pack between fights gets to shape their build.

That single rule creates the crossover the mode needs. Neither path is pure, because the best players do both for different reasons.

### Banish

One charge every `[TUNE: 5]` levels. Permanently removes a card from your pool for the rest of the match. Skill expression through planning: banishing the cards that clog your draft raises the odds of the ones that complete your build.

### The face-down slot

The third card in every draft is dealt **face-down**, showing only a rarity glow. Card art exists: `Card_Art_Ace_In_The_Hole.png`.

Its rarity distribution is shifted up — a face-down card is meaningfully more likely to be Rare or Legendary than the two visible options.

This is a real decision rather than a free upgrade, because **in a survivor-like, fit beats rarity**. A Common that completes an evolution is worth more than a Legendary that does nothing for your build. So the choice is: take the known card that advances the plan, or gamble on raw power that may not fit.

Players who are behind will gamble. Players who are one card from an evolution will not. That is exactly the risk-appetite gradient the mode wants, and it emerges from the players' own situations rather than being imposed.

## 5. Flashpoints and the storm

Left alone, twelve players on a map will spread out and avoid each other. Three systems force convergence on a schedule.

### Buoys

Three capture points using `Voxel Game Assets/world items/Buoys/` art — purpose-built floating markers that already exist.

Holding a buoy gives a passive XP trickle `[TUNE: 2% of a monster kill per second]` and reveals monsters and captains within a radius. Contested buoys stop generating for everyone.

They are worth holding but not worth dying for. Their real function is to be *known locations where someone probably is*, which gives roaming players somewhere to go.

### The Kraken flashpoint

At **5:00**, a world boss spawns at the centre of the map. Default is the Kraken (`world items/World Bosses/Kraken (Urza)/Kraken.gltf`), rotating with Megalodon and Sea Serpent across matches.

The killing blow awards:

- A very large XP payout, on the order of `[TUNE: three captain kills]`
- A guaranteed Legendary card
- A large Plunder scatter that anyone can contest

The boss fights back with telegraphed area attacks that hit everyone.

This is the mode's centrepiece and the answer to "how do we make it entertaining". At 5:00 every captain on the map wants the same thing in the same place, the boss is actively trying to kill all of them, and the killing blow is worth more than anything else in the match. It is a three-way brawl with a timer, and it happens every single game.

The Kraken should be tuned so a single captain **cannot** solo it inside the window. Someone has to commit damage while exposed, which means whoever does the work is not the one who is safe.

### The storm

From **6:00**, a hurricane ring contracts. Outside the ring is escalating damage over time. By **7:30** the play area is small enough that avoidance is impossible.

The storm exists so the last ninety seconds cannot be won by hiding, and so the mode ends in a fight rather than in a clock. Visually it needs new VFX (a P0 item in [02](02-asset-audit-and-gaps.md#p0--vfx)), though `world items/VFX/cloud/` and `wave_crests/` provide a starting point.

## Match timeline

| Time | Event |
| --- | --- |
| 0:00 | Spawn spread around the map edge. Monsters only. |
| 0:00–1:30 | Farming phase. Early levels, first cards. Almost no PvP. |
| 1:30 | Buoys activate |
| 2:00–5:00 | Roaming and skirmishing. First Heat builds. First big Plunder drops. |
| 5:00 | **Kraken spawns.** Match-wide announcement. |
| 5:00–6:00 | The brawl |
| 6:00 | **Storm begins contracting** |
| 6:00–7:30 | Forced convergence, escalating fights |
| 7:30–8:00 | Endgame in a small circle |
| 8:00 | Match ends. Highest Bank wins. |

## Death, respawn, and the ghost

Sinking costs you Plunder and eight seconds. You respawn at a safe map edge with a `[TUNE: 3 second]` invulnerability shield that breaks on your first shot.

**During those eight seconds you are a ghost.** You free-fly the map, see everything, and may place a single **mark** on one captain, visible to all players for `[TUNE: 10 seconds]`.

Three reasons this earns its place. It converts dead time into agency. It lets a victim take revenge by proxy — painting their killer for everyone else is deeply satisfying. And it accelerates the endgame, because deaths generate information that generates fights.

Ghost visuals come from `Voxel Game Assets/Avatar/ghosts/` and the `Pirate Skins/ghost/` set.

## Scoring and the ranked ladder

Final placement is by **Bank**, not by kills. A disciplined farmer who never dies can absolutely win — they simply have to survive the storm with a large Bank while everyone knows they are carrying it. That is a legitimate and difficult way to play, and it should stay winnable.

Rank points are awarded by placement, weighted by lobby rating.

The ladder uses the eleven trophy tiers already in `Voxel Game Assets/Achievements/achievement_pvp_1/`:

wood → bronze → silver → gold → sapphire → emerald → ruby → diamond → master → dreadlord → supremelord

A full competitive ladder's worth of finished art, already tiered. Matching PvP achievement objects exist in `collectibles/achievement_pvp/`.

## Spectacle

The mode has to be entertaining to lose. Specifics:

- **Bounty kill feed** — "Blacksalt has taken Redhand's plunder (4,200)". The number is the story.
- **The cannon salute** — when a Dread Captain sinks, every player in the match hears a distinct cannon volley and sees a screen-wide banner. It should feel like a public execution.
- **Doctrine sails** — red and gold on the horizon, as above. The single highest-value readability feature in the mode.
- **Plunder scatter** — a dying captain's Bank bursts outward in a wide, bright, unmissable spray of orbs. It should look like a treasure explosion, because it is.
- **Signal-flag emotes** — a small taunt wheel rendered as flag hoists. Enough expression for rivalry, not enough for coordination.
- **Legend of the Match** — three end-screen cards: most Plunder stolen, longest survival streak, and the Kraken's killer. Recognises hunting, farming, and opportunism separately, so all three playstyles get a moment.
- **Storm lightning** telegraphed against the contracting ring, so the pressure is felt visually rather than read off a UI element.

## Known failure modes

Stated honestly, with the mitigation and the residual risk.

| Failure mode | Mitigation | Residual risk |
| --- | --- | --- |
| **Everyone farms, nobody fights** | Plunder makes banked XP stealable; storm forces convergence; Kraken creates a scheduled brawl | Low. The incentive structure directly opposes it. |
| **One player snowballs and the match is decided at 3:00** | Heat exposes the leader; deaths keep your build so victims stay dangerous; Plunder transfers value to whoever ends the streak | Moderate. Needs playtesting at high skill disparity. |
| **Third-partying feels unfair** | Deliberately embraced. Half of all Plunder scatters publicly. | Low, but it must be *communicated* as intended, not tolerated. |
| **Solo-queue teaming** | No text or voice chat; emotes are non-specific; Plunder rewards betraying a temporary ally | Real and unsolvable. Every game in this space has it. |
| **Skill gap makes new players miserable** | Rank brackets; never eliminated; every match pays Contribution Score regardless of placement | Moderate |
| **Low population at launch** | Backfill with bots at the lowest ranks, clearly labelled | Real. An 8-player mode needs 8 players. |
| **Predator cards dominate in a duel** | Doctrine sails remove ambush anonymity; Harvesters out-level Predators through faster clear | Needs close balance attention |

## Dependencies

Bounty Waters is the **third** mode built, not the first. It depends on:

- Combat tuned in PvE, because PvP balance on untuned combat is wasted work
- A server-authoritative netcode topology, which is a stack decision with economic consequences — see [08](08-engine-and-tech.md)
- The full VFX and audio backlog, since spectacle is most of what this mode is

## Open questions

1. **Is 8 minutes right?** Long enough for builds to develop, short enough to re-queue. Could be 6 with faster XP.
2. **Should the Kraken be soloable by a fed player?** Currently no. Allowing it would create an enormous power spike and a great story, at the cost of match balance.
3. **Should Doctrine be visible as a number to its owner?** Hiding it keeps the sail reveal dramatic; showing it makes the system learnable. Probably show it after the tutorial.
4. **Duos and trios.** Convoy bonuses from [04](04-modes-solo-and-coop.md) would need rebalancing, and teams change the Plunder economy substantially.
5. **Does the ghost mark need a cooldown across repeated deaths?** A player dying repeatedly could spam marks on the leader.
