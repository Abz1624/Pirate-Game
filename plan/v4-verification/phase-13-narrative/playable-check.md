# M13 playable check (automated part)

Scripted Editor run of `V4SaltwindRun` with a fresh tutorial (`scratchpad/m13play.py`, `m13shot.py`; the playable check restores your prefs, so your real tutorial progress is untouched). Logs in `playable-check/log.txt`, `log-shots.txt`.

| Step | Result |
|---|---|
| Fresh First Conquest | Tutorial step 1 of 9 shows "Sail with WASD or the left stick." in a strip under the timer (`03-tutorial-prompt-in-play.png`); the run is not paused |
| Step completed | Step 2's prompt replaces it ("Your guns fire on their own. Sink ships and sail over the XP they drop.") |
| Barks | The run-start bark plays at 0:03 (director state checked live); a Penny bark shows bottom-left above the loadout with her name over the line, never covering the play area (`04-bark-in-play.png`); a Barnacle King bark via the console |
| Level-up | Barks wait while the level-up cards are open and play when they close (found here and fixed; EditMode test) |
| Not shown | The screenshots used longer display times (a live edit) because each Editor command takes seconds; the default bark stays 3.5 s, prompts 6 s |

## Your playtest

(Pending.) See [TODO-FOR-YOU.md](../TODO-FOR-YOU.md#m13).
