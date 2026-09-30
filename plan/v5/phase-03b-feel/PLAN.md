# V5 M3b: feel fixes (island impact, sinking kegs, ability button)

Added 1 Oct 2026 from the owner's notes b, c and g, built while the owner was away (decisions D3-D5 in
`plan/v5/DECISIONS-WHILE-AWAY.md`). Start commit: `59bee479`.

| Task | What | Where | Status |
|---|---|---|---|
| I3b.1 | A hit faster than 2 m/s into a shore pushes the ship back off it (60% of the impact speed + 3 m/s, fading over 0.5 s, at most every 0.75 s) and costs 5 hull at most every 2 s, through the build's incoming multiplier, with the usual hit feedback | `Foundation/World/IslandObstacles.cs` (the hardest hit of a move), `Foundation/Player/PlayerShipController.cs` (`TryIslandImpact`, `IslandImpactSettings`), `Config/Schemas/RunConfig.cs`, `Runtime/Composition/RunBootstrapSequence.cs`, `Foundation/Combat/DamageReceiver.cs` (`TakeImpact`), `Runtime/Spawning/PlayerDamageService.cs` (`Impact`), `Runtime/Core/V4Game.cs` | done |
| I3b.2 | An unblown keg sinks (1.5 s, down and tilting) and is gone 45 s after it was dropped | `Config/Schemas/WeaponConfig.cs` (`mine.lifeSeconds`, `sinkSeconds`), `Runtime/Weapons/WeaponBehaviors.cs` (`TickKegs`), `WeaponCombat.cs`, `WeaponContracts.cs` (`MineSunk`), `WeaponEffectsForwarder.cs`, `Presentation/Weapons/WeaponVfxPresenter.cs` | done |
| I3b.3 | The Captain's special ability on touch devices: a round button bottom right in the touch stick's art, a radial cooldown shade, a glow when ready | `Foundation/Platform/TouchAbilityButton.cs`, `VirtualJoystick.cs`, `Presentation/Hud/CaptainAbilityButtonPresenter.cs`, `Presentation/V4PresentationRoot.cs`, `Runtime/Composition/BuildModule.cs` | done |
| - | The actives are on in the data (`ExperimentConfig.captainActivesEnabled` 1, `V4SaltWindHudSettings.hideCaptainActive` 0) | checked | - |
| - | v3-parity preset: island impact 0 (V3 slid along the shore), keg lifetime 0 (never) | `plan/v4-verification/presets/v3-parity.json` | done |
| T3b | EditMode: keg lifetime and parity (`V4WeaponM5Tests`), the sinking view (`V4WeaponVfxM6Tests`); PlayMode: `V5FeelPlayModeTests` (an island hit bounces and costs hull; holding into a shore costs hull at most every 2 s through the incoming multiplier; V3 slides along; the button shows only on touch devices, a real EventSystem touch reaches the Captain, it hides and comes back) | `Tests/V4/` | pass |

Evidence: `editmode.json` (374/374), `playmode.json` (56/57: the one failure is the known intermittent
`PowderCache_StillSpawnsThroughTheEncounterExecutor`, unrelated, flagged as a separate task), `playable/` (the button
above the weapon grid, a real press firing All Guns! with a 17 s cooldown, one island hit costing exactly 5 hull, 0
game errors). After the last change (the button placed above the SaltWind weapon grid, which it covered) and the test
fixes (level-up cards answered while counting live physics frames; the parity test asserts no push-off, since how far
the ship slides depends on the random island), `V5FeelPlayModeTests` passed 4/4 three runs in a row and
`V4HudPlayModeTests` 8/8.

## Independent review (v5-reviewer, 1 Oct)

First pass (plan and code): no blockers; plan P1-P12 and code C1-C9. Fixed: C1 the Captain presenters hide through a
CanvasGroup (they deactivated themselves and could never come back); C2 impact damage through the incoming multiplier
without the contact hit immunity; C3 a separate 2 s damage cooldown and a hold-into-the-shore test; C5 a real
EventSystem touch in the test; C6 safe-area insets; C7 the shore hit returned instead of shared statics; C9 assets
saved with the new fields. Found while checking: the bounce was cut by the reverse-speed limit (now a fading push
outside the steering limits); the button was invisible when the SaltWind HUD was off (now on the canvas root). C8
(sinking animates during a level-up pause) accepted: cosmetic.

Re-verification: all plan findings and C1, C3, C5-C7 fixed; new N1 (a non-lethal knock raised no hit event: no feedback,
items not told) fixed with `DamageReceiver.TakeImpact` (the event fires, the hit immunity is neither checked nor
started), which also fixes N2 (a lethal knock inside the immunity); N3 the push loses its part into a shore it hits;
N4 the tests answer level-up cards and V3's slide is checked. The keg parity band is unaffected: the simulator does not
model keg lifetime (kegs blow long before 45 s in a fight).
