# 07 — UI, UX, Controls and Accessibility

Use 1920×1080 reference, scale-with-screen 0.5, landscape, and padding `max(24 logical px,device safe inset)`. Minimum target 48×48; primary touch actions 72+.

## Combat HUD

- Top-left: Captain portrait, Hull bar/value and statuses, ≤30% width.
- Top-center: stage/depth and stage clock; overtime directly below; objective beneath that.
- Top-right: pause button; minimap below, 18% screen height clamped 140–220 px.
- Below minimap: kills, run Gold, score eligibility.
- Left-middle: pinned goal and five-second pressure warnings.
- Bottom-center: Weapons/Logbooks; Items collapse into a scrollable summary.
- Touch: 160 px floating joystick bottom-left; 88 px hold/interact bottom-right above gesture zone.
- Desktop/controller: WASD/arrows/left stick move; E/South interact; Escape/Start pause. Auto-fire has no button.

Choice screens use a full scrim and three cards with current→new value, rarity, cap and binding. Use horizontal cards; below readable width use one-card paging. Only one modal may exist. Ads/store/event prompts never overlay combat choices.

## Screens

Boot/update/consent; guest/login/link/recovery; opening story; home; activity/Captain/biome/tier select; tutorial; combat; pause/reconnect/takeover; results; Territory; Ledger pages; Collection; Contract Board; Defense Director/practice; raid list/quote/report; global/Crew/event standings; Crew roster/join/lock/queued leave; Events; Ship & Cosmetics; Store/restore/refund; Chronicle; settings/support; restricted operator console.

Every network screen implements loading, empty, locked, unavailable, offline, pending, conflict, expired, retryable error, terminal error and success. Never display an optimistic currency balance as settled. Active-run cosmetic/loadout changes apply next run and say so.

## Accessibility and readability

Scalable HUD/text; reduced flash, shake and particles; color-independent shapes; subtitles/text for audio cues; remappable desktop/controller inputs; left/right touch layout option; high-contrast telegraphs; no decoration may mimic loot/hazards. Telegraph render priority is boss lethal > Dread > elite > ordinary attack > damage VFX > cosmetics. Test 16:9, 19.5:9, 4:3 and ultrawide without changing simulation visibility unfairly.

