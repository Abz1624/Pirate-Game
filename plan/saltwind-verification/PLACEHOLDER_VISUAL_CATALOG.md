# Saltwind placeholder visual catalogue (Step 11)

Stable IDs for art replacement tracking. Gameplay roles are player-facing; references are primary code hooks.

| Stable ID | Gameplay role | Current visual | Silhouette / color | Replacement needed | Referenced in |
| --- | --- | --- | --- | --- | --- |
| `poi_salvage_chest` | Paid salvage shop | Placeholder marker + billboard | Cyan box label | Production chest prop + price UI | `POIManager`, `V3PlaceholderFactory` |
| `poi_trial_buoy` | Signal buoy charge | Ring + charge meter | Yellow ring | Animated buoy mesh | `POIManager.TickSignalBuoyCharge` |
| `poi_merchant_skiff` | Powder cache ambush | Skiff marker | Orange label | Skiff + powder barrels | `POIManager` powder cache |
| `poi_boss_beacon` | Treasury boss summon | Beacon telegraph | Red pulse ring | Lighthouse / beacon asset | `POIManager.SummonBoss` |
| `ui_run_results` | End-of-run summary | Gray panel + TMP | Neutral panel | Full-screen results art | `V3RunResultsScreen` |
| `ui_main_menu` | Captain select | Carousel placeholders | Dark teal backdrop | Production menu layout | `V3MainMenuController` |
| `vfx_hostile_telegraph` | Incoming attack warning | World rings / toasts | Red/orange | Water-readable telegraphs | Boss director, POI telegraphs |
| `vfx_floating_damage` | Hit feedback | TextMesh numbers | White/yellow enemy, red player | Styled damage VFX | `FloatingDamageNumbers` |
| `vfx_storm_ring` | Storm safe boundary | Saltwind ring visual | Blue shrinking circle | Storm wall VFX | `V3SaltwindStormRingVisual` |
| `powerup_floor_drop` | Random floor pickup | Placeholder orb | Kind-specific tint | Per-kind pickup mesh | `V3FloorPowerupWorld` |
| `weapon_hud_icon` | Loadout slot icon | UI/Weapons PNG or fallback | Weapon-colored | Final icon set | `V3RunHudIconResolver` |
| `enemy_identity_label` | Debug identity (playtest) | Floating label | White text | Off in shipping | `EnemyIdentityDebugLabels` |

Telegraph readability rule: hostile warnings must remain visible above water clutter; player damage numbers are never rate-limited (enemy numbers capped per frame in `FloatingDamageNumbers`).
