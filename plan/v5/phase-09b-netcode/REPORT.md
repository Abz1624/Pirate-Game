

## Enemy stutter fix and leash outside the view (10 Oct)

After the owner's re-test of `e01a2e87` ("the enemies seem to be stuttering now a bit"). Cause, fix and tests: `README.md`,
"Enemy stutter fix and leash outside the view". Protocol unchanged (5); rules v5 (same bounds). Files in `enemy-stutter-fix/`.

| Measure | Before (e01a2e87) | After | Where |
|---|---|---|---|
| On-screen enemy jitter in normal sailing after picks, mean / p99 (m/s); e1f56626's clock 0.142 / 1.370 | 3.176 / 28.96 | 0.131 / 1.014 | in-process, 270 ms, 60 fps |
| Frames drawn past the newest snapshot after picks | 49.9% | 0% | same |
| On-screen enemy speed jumps (over 3 m/s in a frame) after picks | 24.3% of samples | 0.03% | same |
| Frames the enemies stood still after a pick (e1f56626: 48) | 0 | 0 | same |
| Enemy jitter mean / p95 in local Chrome, 270 ms (3 voyages each) | 0.15-3.46 / 0.46-24.3 | 0.11-0.16 / 0.26-0.74 | `webgl/` |
| Render delay at the end of a voyage, local Chrome | 173-350 ms | 144-197 ms | `webgl/` |
| On-screen frames faster than any enemy sails (leash pop-ins), local Chrome | 24-116 a voyage | 0 | `webgl/` |
| Leash relocations in view, all headings, 4:3 to 2.4:1 screens | 34.5-65.3% (31 m rule, standing) | 0 of 163,080 (4 m clear past the hull) | EditMode |
| Local Chrome with 3 tab hides | - | 0 resyncs, 0 on-screen pop-ins, jitter 0.121 / 0.443 | `webgl/` |
