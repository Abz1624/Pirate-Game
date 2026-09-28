# Your TODOs (updated 29 Sep, after playtest round 1)

## Where things stand (29 Sep)

M10–M13 are committed; your round-1 feedback is applied in one commit on top ("V4 M10–M13: playtest round 1").
**M14 and M15 are not started** (your call: pause for playtests; M15 not yet). The M14 inventory is ready; when you
are happy with M10–M13 we choose between a "core switch-over first" M14 and the full M14.

Evidence: `playtest-m10-m13/measure/RESULTS.md` (time-to-death with the human-like autopilot), `playtest-m10-m13/parity/`
(V3-comparison traces), EditMode 292/292, PlayMode 50/50. An independent reviewer checked the round; its findings are fixed.

## Round 3 (29 Sep) — your answers

- **Gold curve:** 15 was too low for big efforts. Now scaled to effort against the other Rum rows: `60 × (1 − ½^(gold ÷ 1,500))`
  — 100 → 2, 500 → 12, 765 → 17, 1,500 → 30 (≈ a boss victory; ~15–25 min of strong play at 30–110 gold/min), 3,000 → 45,
  10,000 → 59, never above 60 (≈ what a good run earns from all other rows). Early gold pays about like active minutes
  (~1 Rum per 40 gold).
- **M14:** the full switch-over (all 59 legacy adapters). Started.
- **Chipmunk:** Chippy only; the "Name the Chipmunk" community event is dropped from the GDD.

- [ ] **Check the gold curve** once more when convenient (die with 500–1,500 gold).

## Playtest round 2 (29 Sep) — your answers

Everything confirmed except the gold → Rum rate ("too generous; make it flatten so overtime gold farming can't break
the economy"). **Done:** leftover gold now pays `15 × (1 − ½^(gold ÷ 350))` Rum, rounded down — 100 gold → 2, 250 → 5,
350 → 7, 500 → 9, 765 → 11, 1,500 → 14, and never more than 15 however much is farmed (15 is also the overtime row's
cap). Before: 1 per 10 gold (765 gold paid 76 of a 95-Rum run). Tunable per row: `maxRum`, `goldForHalf`.

- [x] **Check the gold curve:** 15 max felt too low — retuned in round 3.

## Playtest round 2 — what you tried (15 min)

1. **Weak vs strong (your main bug).** Weak: keep one weapon, only upgrade it. Pressure (F1 → "difficulty") stays at
   1.00 after 30 s and should now *stay* there even if that weapon is maxed. Strong: take every new weapon. Pressure
   should climb to ~1.25–1.35 by minute 3–4; you should see clearly more enemies (up to ×1.75 spawn rate), some elites
   in the ambient waves (they pay elite rewards now), tougher ambient ships, and a tougher Reef Crab / elite beats.
2. **Die with gold left:** the results screen shows only "RUM EARNED +N" (leftover gold is part of it, on the flattening
   curve) and the treasury, then "Survived m:ss · kills · level", gold earned/spent/left and the build.
3. **Win and continue, then die:** Rum is paid once, at death.
4. **Barks:** lines are from your Bible; Chippy's captions; your Captain has their own lines. Long lines wrap now.
5. **Tutorial (F1 → Reset tutorial):** sail, XP and level-up never skip; chest and landmark skip after 2.5 min.
6. **Boss kill:** first conquest ever → "TERRITORY CLAIMED — SURVIVE AS LONG AS YOU CAN."; later → "OBJECTIVE SECURED".

## Decisions I made in round 1 (you confirmed them in round 2)

- [x] **Power index overkill cap:** each hit counts at most 1.5 × a typical enemy's current HP, so one huge weapon
      no longer reads like six (this was why a one-weapon run could sit at 1.35). Tunable: `overkillCap`,
      `referenceEnemyHp`, `overkillCapMultiplier`.
- [x] **Offense gain 0.25 → 0.8:** a build 30 % ahead of on-track reads ~1.24; 45 % ahead reaches the top (1.35).
- [x] **On-track curve** measured from three average autopilot runs: ×4 by 1:00, ×8 by 3:00, ×10 by 6:00, ×24 by 10:00
      of the starting offense. It is the "Pressure 1" line; your playtest decides if average should feel harder.
- [x] **Stronger levers:** threat up to ×1.75, elites up to 12 %, tier timing up to 90 s early, a new toughness lever
      (ambient HP ×1.25 / damage ×1.2 at the top) and a focus lever (Reef Crab and elite beats HP ×1.5 / damage ×1.2;
      their timing never moves).
- [x] **Gold → Rum rate:** too generous — replaced by the flattening curve above.
- [x] **Victory that ends the run** (only if "continue after boss victory" is switched off) pays nothing and says
      "RUM EARNED +0 (Rum is paid when the ship sinks)".
- [x] **Autopilot (DevTools only):** F1 → Run → Autopilot Average / Weak / Strong / Off, if you want to watch it play.

## Not measured well (known limits)

- Time-to-death with a bot is noisy (the bot dies to bomb blasts and the Barnacle King in every build), so it does
  not prove that strong builds die sooner; Pressure and spawn rates do separate clearly. Your playtest is the test.

## Earlier questions — all answered in round 1

Bible (read, never edited or committed) · expected offense (designed for fun) · defense/performance/gains OK · crit and
evolutions counted · early game only 30 s, then Pressure ≥ 1 · threat band OK · elite rewards on · minibosses/elites
adapt · measurement done · Rum rows OK, paid only at death · damage share OK · results UX simplified · barks from the
Bible, Chippy, Captain as 4th speaker · tutorial gate/placement OK, skips only on optional steps · banner rule agreed.
