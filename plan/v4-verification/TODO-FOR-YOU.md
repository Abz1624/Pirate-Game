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
