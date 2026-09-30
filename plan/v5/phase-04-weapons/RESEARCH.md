# V5 M4 — groundwork found before building (30 Sep 2026)

A read of the V4 weapon pipeline before adding Anchor Flail, Chainshot, Siren Bell and Ramming Prow and the eight
missing evolutions. Things the plan did not know:

- **Slows do not exist.** The harpoon's "tar" stores a status that no movement code reads (and it decays in 0.25 s).
  Chainshot's 25% slow and Siren Bell's 20% slow need a real slow status (multiplier + duration) read by enemy movement.
- **Pierce does not exist.** Every projectile hits once (`PlayerProjectiles.cs:91`); the Pierce upgrade axis does
  nothing. Chainshot's two pierces need a pierce field and a per-projectile hit set.
- **Weapon knockback is not configurable.** `WeaponConfig.knockback` and the Knockback axis are never read; only a
  generic hit nudge exists. Anchor Flail's 2 m knockback needs a real knockback.
- **Aura, Trail and Summon behaviours are ported but unused**, so untested in play. Aura has no visual at all and
  needs a target to fire; Siren Bell needs a pulse effect and `NeedsTarget = false`.
- **Only one Orbit weapon works at a time** (the first orbit slot): Anchor Flail as an Orbit weapon would stop Flying
  Cutlasses. It needs its own slot handling or its own behaviour.
- **Boost has no event or state for weapons**: the Ramming Prow needs `IsBoosting` on the ship state and a behaviour
  that hits enemies in contact while boosting.
- **New evolution effects** need an enum value, a config block, behaviour branches and new chest-pool items with icons.
- **Guard tests** pin 10 weapons, 6 evolutions, 19 items, the aim-overlay palette, the icon catalogue and the
  level-damage-factor set; they must be extended, not bypassed.
- **No weapon shop in the V4 menu**; Rum unlocks go through the results screen's goals.

Open decisions from M2/M3 that affect M4: the evolution level (8 or 10), whether synergy items stay as they are, and
the volley/crit pieces — M4's new evolution items should follow those answers.
