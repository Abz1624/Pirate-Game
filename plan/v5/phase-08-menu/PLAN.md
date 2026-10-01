# V5 M8: main menu, shop and inventory (phase plan and results)

Plan: `V5_PLAN.md` "#### M8". Start commit: `30b2e2e6` (M7). Built after the owner's "continue M7 and M8" (1 Oct).

| Item | What | Where |
|---|---|---|
| I8.1 Meta services | `RoguePirates.V5.Meta`: `IAccountService`, `IEconomyService` (RUM balance, spend), `IInventoryService` (owns, owned per category), `IStoreService` (entries, buy). `LocalMetaServices` over the existing save; a purchase goes through `SaveService.TryPurchaseMarksUnlock`, so the V4 M16 guard (a stale save never drops ownership) stays. `PurchaseResult`: Bought, AlreadyOwned, NotEnough, NotForSale, Locked; one message per result (no em dashes) | `Runtime/Meta/MetaServices.cs` |
| I8.2 Catalogue | `StoreCatalogConfig`: category, rarity, "new", sale share, requirement, sort order and the web2/web3 origin (D4) per content id. Prices stay in one place each (`UnlockConfig` for weapons, Logbooks and items; `CaptainConfig.rumCost`). No catalogue asset yet: without one every entry is web2, not new, not on sale (adding it later changes no code) | `Config/Schemas/StoreCatalogConfig.cs` |
| I8.3 Hub | Generated over the art menu: SET SAIL opens the mode select (Voyage; Island Raids, Island Race, 1v1 Arena, Hunt shown as Coming), Voyage opens the art team's Captain select; LEDGER opens the shop (tabs Weapons, Logbooks, Items, Captains; Buy N RUM, Owned, Not enough RUM), PROFILE the profile (RUM, runs, best time, owned counts); Collection lists what you own per category; SETTINGS opens M7's settings. 72-unit buttons, a scroll list | `Presentation/Menu/MenuHubPresenter.cs`, `ArtMenuView`, `V4MainMenuController` |
| I8.4 Pools | Only owned priced content enters the run's offers and chests (`BuildModule`, since M4/M5); the shop sells only content that is in a pool (D34 keeps Ironclad and Cursed Coin out until armor and gold gain work); starters are free and never sold. `UnlockService.CostOf` and `DisplayName` cover Logbooks and items too (its Purchase path and messages) | `MetaServices.InAPool`, `UnlockService` |
| T8 | `V5MetaShopTests` (5): every priced thing sold, starters not, held-out content not; a buy spends the price once, needs enough, a second buy does nothing; a Captain keeps its weapon and gold locks; ownership survives a stale save; the hub shows the shop, buys through it, touch-sized buttons, the list not clipped, drawn above the art menu | `Tests/V4/EditMode/` |

Scripted menu check (`plan/v5/tools/menu-check.ps1`, `menu-check/`): boot to the menu, 1,000 RUM granted (the save is
backed up and restored exactly afterwards), the Logbook shop (4 for sale), Fortune bought (200 RUM), the collection,
the mode select from SET SAIL, the Captain select, a run started with Fortune owned; screenshots at 1920×1080 and
2400×1080; 0 game errors.

Found by the check and fixed: the list was 100 units wider than its view (a stretched RectTransform's default size), so
every label lost its first letters (the settings rows had the same bug); the shop sold Ironclad (D34).

Not built (recorded): a "new" badge and sales need a catalogue asset (owner's choice of what is new); the Captain carousel
stays the art team's select; decorations and sails come with their content; equip happens in the run's choices as today.

## Tests

Full suites once (before the review fixes): EditMode 462/463 (the architecture guard: the menu controller reached
`Ports`, fixed by moving the meta services into `MenuServices`), PlayMode 60/60. After the fixes, the classes they touch:
`V5MetaShopTests` 5/5, `V4HudM6Tests` (the guard) 15/15, `V4RewardsM12Tests` 13/13, `V5MobileUiTests` 11/11; the menu
check again (0 game errors).

## Independent review (v5-reviewer, 1 Oct)

No blockers; 4 majors, 6 minors. One pass.

| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | Major | A Captain bought in the shop stayed locked in the Captain select until the scene reloaded | The balance-changed handler refreshes the select and its focus, as a buy there does |
| 2 | Major | The shop ignored a Captain's weapon and lifetime-gold locks (latent: today's Rum Captains have none) | `Buy` asks the menu's Captain locks (`CanBuy`); test. One money path for both buy buttons: recorded for M10 |
| 3 | Major | The interface is not ready for the server: the presenter uses the concrete class, `Buy` is synchronous, prices have no currency, profile stats read PlayerPrefs | Recorded for M10/M11 (they change the interface anyway: async buy with a request id, Pending/Failed, a price currency, stats behind the account) |
| 4 | Major | No PlayMode test of "buy, start a run, see it offered"; the check shows ownership, not the offer | Recorded: the offer pools gate on ownership in `BuildModule` (tested since M4/M5); the owner's playtest checks the offer |
| 5 | Minor | Collection unreachable | "Your collection" row in Profile |
| 6 | Minor | Gamepad could not reach the hub; the art menu's margins stayed clickable | Close is selected on open; an invisible click blocker covers the margins |
| 7 | Minor | A stale comment on the catalogue cache | Fixed |
| 8 | Minor | `UnlockService.DisplayName` showed raw Logbook and item ids | Resolves them |
| 9 | Minor | The check might leave the test's RUM in the live save | No change: the restore runs after play mode ends (AppRoot gone), so nothing writes the in-memory save afterwards |
| 10 | Minor | No full suite results yet | Above |

Questions for the owner: Captains with an unmet weapon or gold lock in the shop (built: shown with their price, a buy
says "still locked"; or hide them?); where Collection lives (built: from Profile); which content gets a "New" badge or a
sale (a catalogue asset).
