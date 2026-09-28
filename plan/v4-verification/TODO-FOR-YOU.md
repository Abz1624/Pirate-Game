# Your TODOs (written while you were away, 28 Sep)

You asked me to run M10 → M15 unattended, log my doubts and anything that needs your input here, and carry on with what I judged correct. Each phase below is committed "(awaiting playtest)"; nothing waits on you to continue, but everything here needs your answer or check.

How I ran it: each phase got its targeted tests, an automated playable check, an independent read-only review (findings fixed), and its own commit. The single V3-comparison trace set ran only for phases that replace V3 gameplay. Only the pause for your playtest was skipped. M15 (deleting V3) is on a separate branch and not merged.

## Standing questions

- [ ] **Narrative Bible (M13):** may I read your untracked `Rogue_Pirates_Narrative_Bible_v1.md` as the source for bark and tutorial lines? (Until you say yes, M13 uses placeholder lines written from the gameplay doc; I have not opened the file.)

## Per phase

### M10

Committed "(awaiting playtest)". Evidence: `phase-10-director/CHECKLIST.md`. Traces: exact V3 match (Observe changes nothing).

**Playtest (5 min):** F1 → search "difficulty". Weak run (one weapon): Pressure should sit around 0.9. Build section → add weapons: it should climb smoothly toward ~1.3 and never pass 1.35. Press Off: it snaps to 1.00. Nothing in the run should feel different (Observe only).

**Doubts I resolved myself (please confirm or overrule):**
- [ ] *Expected offense* = the run's own first offense × the enemy HP growth curve (the plan said "generated from the V3 traces", which have no weapon stats). So Pressure 1 means "your damage keeps up with enemy HP growth".
- [ ] *Defense* = hull fraction ÷ incoming multiplier (plan said ×, which would make Brace! read as weaker).
- [ ] *Performance* "even" point = 90 % of spawns killed over 30 s; hull loss costs 0.02 per hull-fraction/s. With nothing spawning (boss arena) it counts as even.
- [ ] *Gains* offense 0.25 / defense 0.15 / performance 0.2 (my guesses; a six-weapon build at 1:30 hits the 1.35 cap, one weapon sits at ~0.9). Tune in M11 once the levers act on it.
- [ ] The power index ignores crit and evolutions for now.

### M11

Committed "(awaiting playtest)". Evidence: `phase-11-adaptive/CHECKLIST.md`. **Adaptive is now the default for every run**; the v3-parity preset keeps Observe (traces prove it equals V3).

**Playtest (10 min):** F1 → search "adaptive".
1. Weak build (keep one weapon, play normally): Pressure and "threat rate" should sit a little below 1 — fewer enemies than V3.
2. Strong build (Build section → add weapons): Pressure climbs to ~1.3 over a minute; the lever line shows threat ×1.3, tier lead ~55 s, cap HP up to ×1.2, composition on. More enemies, occasional elites among ordinary spawns.
3. Tick "Force Pressure", drag to 1.35: the ambient spawn rate rises about a third. Switch the mode to Observe: the levers go neutral.
4. Die: nothing visible yet (the run record is stored for the future leaderboard; it shows in M12/M14 UI only).

**Doubts I resolved myself (please confirm or overrule):**
- [ ] **Early game for everyone:** at the start every player has one weapon; with an average pilot Pressure drifts to ~0.9, so the first minutes have ~10 % fewer spawns than V3. A good player who kills fast stays near 1. Keep (the doc wants weak builds eased), or never let Pressure go below 1 in the first N minutes?
- [ ] **Threat band:** 1 + (Pressure − 1), clamped 0.85–1.35 (−15 % … +35 %), matching the doc's "±15–35 %".
- [ ] **Elite lever:** up to 5 % of ambient spawns at the top of the band become elites (Rich look, HP ×1.5, damage ×1.1, size ×1.15) but **pay the unit's ordinary rewards** (no Rich gold, no forced powerup, no elite-kill item procs), so "per-kill rewards never adapt" holds strictly. The doc also calls elites "richer targets"; if you want them to pay elite rewards, it is one switch. Its roll uses its own seed-derived stream.
- [ ] **Tier timing:** only earlier (up to 60 s at the top), never later for weak builds.
- [ ] **Composition:** enemies get roles from their attack pattern (shooters/hazard layers = "ranged", rammers/biters/bombers/chargers = "swarm"); a close-range build (cutlasses, flamethrower, kegs, trail, aura) boosts ranged ships' pick weight up to +60 %, a long-range build boosts swarms. The spawner pressure-weight curves are flat (1) by default, so they do nothing until authored.
- [ ] **Cap HP:** ambient spawns made within 5 s of the cap blocking a spawn get up to +20 % HP.
- [ ] **What never adapts:** boss, Beacon/Powder Cache encounters, Reef Crab and elite beats, swarm pulses, overtime and the storm (all stay as authored). Overtime reading the threat lever is a possible later change.
- [ ] **Ranked flag:** Adaptive + version `director-1` + never forced + no live tuning edits (a preset counts as edits, so preset runs are unranked) + the dev console never opened. Checking the config hash against a published list needs the leaderboard backend (later).
- [ ] **Not measured:** the plan's "time-to-death band for strong/weak builds" needs long full runs, which we agreed to avoid; I recorded the lever values over 2.5 minutes instead. Want a one-off measurement later?

### M12

Committed "(awaiting playtest)". Evidence: `phase-12-rum/CHECKLIST.md`.

**Playtest (5 min):** play past 2–3 minutes killing steadily, use a landmark (Signal Buoy / Repair Wreck / Treasure Chart), then die. The results screen should say **Rum** everywhere, list the rows (Active minutes, Special discoveries, Scheduled elite, Boss victory, Overtime) and your treasury; the toast says "Rum settled: +N" (no more "gold banked"). Then F1 → search "rum": tick "1 Rum per chest opened", open a chest, die: a "Chests opened" row appears. Retry: no double pay.

**Doubts I resolved myself (please confirm or overrule):**
- [ ] **Row values = V3's Marks** (2 per qualifying minute of ≥10 kills, 3 per special landmark, 5 per scheduled elite (max 2), 25 boss, 3 per overtime minute up to 15): the 57 fixture holds. The chest row is off by default.
- [ ] **When Rum is paid:** at death (as V3), and — new — when a victory ends the run (only if "continue after boss victory" is switched off; V3 paid Doubloons there instead). Leaving a run mid-way (restart, menu) pays nothing, as in V3.
- [ ] **Results damage share** is the same as V3's (per-hit records, V3's names), now held by V4 instead of V3's static ledger.
- [ ] The results screen keeps V3's layout (only the wording changed).

### M13

Committed "(awaiting playtest)". Evidence: `phase-13-narrative/CHECKLIST.md`. **All bark lines are placeholders I wrote in the GDD's voice** — your Narrative Bible was not read (see the standing question at the top).

**Playtest (10 min):** F1 → search "tutorial" → Reset tutorial, then restart the run. The nine prompts should appear one at a time under the timer as you play (sail, XP, level-up, chest, landmark, Beacon, boss telegraph, "TERRITORY CLAIMED…", the death summary). Barks appear bottom-left (Penny, the chipmunk's action captions, the Barnacle King, your Captain) and never pause anything. F1 → Barks: play any bark.

**Doubts I resolved myself (please confirm or overrule):**
- [ ] **Placeholder lines** (30 barks). Replace from the Bible once you allow it; the chipmunk has no name yet ("The chipmunk").
- [ ] **Tutorial gate:** shows until finished (progress kept across runs under `v4_tutorial_progress`), not only on the first run; V3's four onboarding toasts are retired in V4.
- [ ] **Stalled steps are skipped** after a time limit (XP 2 min, level-up / chest / landmark 2.5 min, Beacon 6 min, telegraph 2 min) so the tutorial never gets stuck; skipped steps don't come back. If you reach the Beacon or sink the boss early, the steps in between are skipped and the claim banner still shows.
- [ ] **A fourth speaker:** your Captain also barks (their catchphrases: Rook "Exactly as planned.", Hank "No." …). The plan listed Penny, the chipmunk and the Barnacle King only. Keep?
- [ ] **Placement:** barks bottom-left above the loadout, prompts in a strip under the timer (their own places, so they never fight the one-message toast). They hide behind the level-up card screen.
- [ ] **The boss-kill toast** ("TERRITORY OBJECTIVE SECURED…") still shows; during the tutorial the claim prompt shows too. Which banner wording wins is your open GDD question.
