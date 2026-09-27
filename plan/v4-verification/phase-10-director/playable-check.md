# M10 playable check (automated part)

Scripted Editor run of `V4SaltwindRun` (`scratchpad/m10play.py`, god mode on), log in `playable-check/log.txt` and `log-graph.txt`.

| Step | Result |
|---|---|
| One weapon (Swivel Guns), 0:28–1:02 | Pressure 0.887–0.901 (offense 11.1, ×0.90–0.96 of the curve; performance 0.43–0.67 while the pilot idles) |
| Six weapons given at ~1:05 | Offense 69.9 (×5.6); Pressure climbs 1.03 → 1.32 over ~50 s, limited by the 0.02/s slope, and stays under the 1.35 clamp |
| Console | Header "Pressure 1.33/1.34" (`01-difficulty-section.png`); Difficulty section with parts, the graph and Off/Observe/Adaptive (`02-difficulty-graph.png`) |
| Observe | Spawns unaffected (trace set: exact V3 match) |

## Your playtest

(Pending.) Try: F1 → search "difficulty"; play a weak run (one weapon) and a strong one (Build section → add weapons) and watch the graph; switch Off and back.
