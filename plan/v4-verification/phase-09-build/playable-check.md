# M9 playable check (automated part)

Scripted Editor run of `V4SaltwindRun` on the V4 build modules (`scratchpad/m9play.py`), god mode on, screenshots in `playable-check/`, full log in `playable-check/log.txt`.

| Step | Result |
|---|---|
| Run start | Captain Rook from `CaptainConfig`, Swivel Guns (First Conquest rule), base hull 100 and speed 6.2, level 0, 0/5 XP (`01-run-start-hud`) |
| XP grant → level-up | The kept V3 modal shows V4 cards with V3's texts: Swivel Guns upgrade (+7% Knockback), Deck Cannons new weapon (stat summary), Scavenger's Net Logbook "+12.5% Pickup Range" (the adopted stat, D6) (`02-level-up-cards`); picking a card resumes the run |
| Kill drops | XP, gold and health barrels burst into flying pieces and pay: level 1 → 2, gold 0 → 160, hull 70 → 100 (`03-drops-bursting`) |
| Magnetic Tide | Dropped at the ship: far gold barrels open and pay (gold 160 → 262), level → 6 through chained level-ups (`04-after-magnetic-tide`) |
| Captain active | Rook's All Guns! fires (18 s cooldown) (`05-captain-active-toast`) |
| Item | Grapeshot Pouch: damage ×1.1, toast shown, item row updated (`06-item-toast`) |
| Battle Signal | Damage 1.1 → 1.375 (×1.25) while it lasts |
| Console | No game errors (only CLI pipeline timeouts and edit-mode logs from the EditMode suite) |
