# M13 — Barks & Tutorial

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `ccfaafc2` (M12, awaiting playtest) |
| Status | **Implemented, awaiting playtest** (28 Sep; questions in [TODO-FOR-YOU.md](../TODO-FOR-YOU.md)) |

The lines are **placeholders** written in the GDD's voice (Penny dry and tired, the chipmunk silent — action captions — the Barnacle King petty, the Captain's own catchphrases). The Narrative Bible was not read (permission pending).

## What was built

| Task | What |
|---|---|
| G13.1 `NarrativeConfig` | `narrative.saltwind` (`V4/Data/Narrative/SaltwindNarrative.asset`, `V4NarrativeConfigGenerator`): 30 barks {id, speaker, trigger (run start, kill streak, level-up, chest, landmark, Beacon hint/spotted, boss summoned/defeated, phase start/warning by phase id, hull low, gold, quiet), value, window, tag, lines, cooldown, priority, once per run}; global cooldown 10 s, 3.5 s on screen, 6 s quiet after the boss; the nine tutorial steps {prompt, completes on, skip after, show on complete}. Phase ids for tagged barks come from the timeline mapping (generator), not schema code |
| I13.1 `BarkDirector` | Event-driven (`Runtime/Narrative`): due barks per trigger, highest priority wins when the global cooldown allows; per-bark cooldowns, once-per-run milestones wait out the global cooldown; nothing plays while paused (due barks wait — a level-up bark plays when the cards close) or after the run; lines picked from its own seeded stream; the Captain speaks with the Captain's title. Console `IBarkControl.Play` |
| I13.1 `TutorialDirector` | Replaces V3OnboardingDirector (its three calls removed from V4): strict order (only the current step's trigger counts), progress under `v4_tutorial_progress` (a later run resumes), skip timers so it never stalls (skipped ≠ completed), claim banner and death summary shown on completion; move / XP / boss telegraph polled from the views; off in the Module Lab. Console `ITutorialControl` |
| Presenter | `NarrativePresenter`: bark caption bottom-left (speaker over line), tutorial strip under the timer; own places, no raycasts, fade on unscaled time; reads only `INarrativeView` |
| Console | Barks & Tutorial section: play any bark, each step's state, reset, complete current |
| Saves | `v4_tutorial_progress` pinned "done" in trace players, snapshotted by the Editor trace session, restored by the PlayMode isolation |

## Tests

EditMode `V4NarrativeM13Tests` (6): config (≈30 barks, every speaker, silent chipmunk captions, nine steps, the claim and death prompts); strict order through all nine steps (events for later steps do nothing; the claim prompt only on the boss kill; saved progress; a finished tutorial never returns; a later run resumes); skip without completing, off in the lab; barks — priority, global cooldown, quiet while paused and the due bark after the pause, nothing after the run, milestone bark after a cooldown; kill streak, once per run, replay by seed, console play; scan: the narrative never pauses or sets run state, and no V4 code calls V3OnboardingDirector. PlayMode `V4NarrativePlayModeTests` (1): in the real scene a fresh tutorial shows step 1, completing it shows step 2 and saves progress, a console bark renders, nothing pauses, no V3 onboarding component.
Suites (after the review fixes): EditMode 289/289, PlayMode 50/50.

## Trace set

None: M13 is presentation only (barks and prompts never touch the simulation, pauses or RNG streams; trace players pin the tutorial as done), and the standing rule runs no traces for presentation phases.

## Playable check

[playable-check.md](playable-check.md).

## Independent review

A read-only reviewer checked the phase. Confirmed: no gameplay impact (own seeded stream, no pauses, state writes or event publishing), the new save key handled everywhere, interface-only registrations, event payloads match the triggers, presenter layout clears the Captain banner and results panel. Findings and fixes:

| # | Finding | Fix |
|---|---|---|
| 1 | Major: using the Beacon or sinking the boss before those steps were current stalled the tutorial (old prompts nagging through the boss fight; the claim banner never on the real first conquest) | Catch-up: the steps in between are skipped and the milestone completes (claim banner shows); a step whose trigger already happened this run completes as soon as it is current; test |
| 2 | A restart or menu exit completed the death summary | Only a real run end completes it; test |
| 3 | "Skipped" was documented as "not completed" but never retried | Comment fixed: a skipped step is not shown again (logged as a decision) |
| 4 | Per-tick closures and list allocations in the bark director | Hand-written loops |
| 5 | Test gaps (a streak assertion that could not fail; no per-bark cooldown, hull, tag tests) | Barks asserted by id; per-bark cooldown, hull-low, phase-tag, level-up-over-cards and catch-up tests |
| 6 | An untagged phase bark fired on every phase | Untagged phase barks never fire |
| 7 | The level-up prompt could not show over the cards (HUD canvas below the modal); step 3 completed before the cards closed | Presenter on the run canvas, last; the prompt shows when the cards appear and the step completes when they close |
| 8 | Some barks explained systems (GDD rule); a fourth speaker (the Captain) not in the plan | Three lines rewritten; the Captain speaker kept — a decision for you |
| 9 | The boss-victory bark repeated the claim banner at the same moment | The quiet window covers every bark; the line no longer repeats the banner |

Re-verified: EditMode 289/289, PlayMode 50/50.

## Incident

A first version of an EditMode test helper looped forever on an ended run and froze the Editor; Unity had to be closed and reopened (nothing lost; the helper is now a bounded loop).
