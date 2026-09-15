# Performance budgets (provisional — M0)

Targets from technical design; replace with measured values after profiling.

| Platform class | FPS target | Initial download | Heap budget |
|---|---|---|---|
| Minimum mobile | 30 | 20 MB | 256 MB |
| Recommended mobile | 60 | 40 MB | 384 MB |
| Desktop browser | 60 | 25 MB | 512 MB |
| Native desktop | 60 | 50 MB | 512 MB |

## Stress cases to profile

1. Saltwind water + 80 pooled enemies + 40 projectiles
2. Overtime spawn replacement at cap
3. Minimap fog update every frame
4. Level-up modal pause/resume

Record results in `plan/performance_results/` after device runs.
