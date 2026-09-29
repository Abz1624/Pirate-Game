# V5 M0 — Setup and baseline: progress

Started 29 Sep 2026. Phase start commit: `f1b9a4cf` (tip of `v4-modular-architecture`), branch `v5-live-game`.

| Step | Content | Status |
|---|---|---|
| C0.1 | Branch `v5-live-game` created from `v4-modular-architecture` | Done |
| C0.2 | Reviewer agent `.claude/agents/v5-reviewer.md` (read-only; plan and code modes). It registers as an agent type in new sessions; in this session it runs through a general agent that loads the same file | Done |
| D0.1 | `plan/v5/V5_PLAN.md` (26 phases, M0–M25), research in `plan/v5/research/`, `TODO-FOR-YOU.md`, tracking artifact | Done |
| T0.1 | Baseline suites in the open Editor (Unity 6000.3.11f1) | Done: EditMode **307/307** (32.7 s), PlayMode **49/49** (177 s); `evidence/baseline-editmode.json`, `evidence/baseline-playmode.json` |
| T0.2 | Plan review by the independent reviewer; findings fixed in the plan | See below |
| D0.2 | GDD: Decision log rows, V5 roadmap, new questions on the board | See below |

Console at start: 0 errors, compile clean (older test-run errors in the log history only).

## Plan review (rev 1 → rev 2)

The reviewer (run through a general agent loading `.claude/agents/v5-reviewer.md`, read-only) found **3 blockers, 12
majors and 7 minors** and confirmed the plan's claims about the existing code. All 22 are addressed in plan rev 2; the
full table (finding and what was done) is in `plan/v5/status.json` → `reviews` and on the tracking page.

Main changes in rev 2: a replay-verification spike (M9) before any account holds value; run RUM pending until verified
(D6); item-origin rule so RUM never leaks into tokens (D4, Q24); anti-collusion (D15, Q27); wager formats per mode
(M24); M1 split into flow (M1) and model (M2); netcode spike (M21) split from the Arena (M22); NFT migration moved to
M26; AI brain moved out of DevTools (M16); raid schedule as a snapshot overlay (M14); .NET 10 LTS (M10); every phase has
an automated check and an owner playtest or check. The plan is now 29 phases (M0–M28).

## Plan re-review (rev 2 → rev 3, 30 Sep)

No blockers; 5 majors and 6 minors (plus two leftovers from the first review), all addressed in rev 3: server-issued
seeds for every value-earning run (D6); migrated local RUM becomes capped, store-only legacy RUM (M10b); fallback B
explicitly amends D6 and fallback A has its own conditional phase M9b; M10 split into M10/M10b and M26 into M26/M26b;
an early netcode measurement in M9 and a no-go path plus a multi-ship runtime task in M21; a standing replay-determinism
test from M10 on; cross-references fixed. The plan is now 31 phases plus the conditional M9b. Table in
`status.json` → `reviews[1]`.

Gate status: automated checks done (baseline green, both reviews closed). **Waiting for the owner's approval of the plan
and answers to the blocking questions (V5-Q1, Q18, Q20, Q21, Q22).** Nothing is committed yet; the M0 commit follows
approval.

## GDD (D0.2)

Three Decision log rows (V5 direction; reversals; two economies and custody, all "Approved, pending"), the roadmap's
V5 track with a stage table, the Not now and Never lists and the business-model note updated, and 27 V5 questions
added to the "Questions for you" board (V5-Q1 … V5-Q30).
