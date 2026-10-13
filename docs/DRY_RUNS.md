# End-to-end dry runs

Every row runs the whole session: launch screen (or a direct Editor dev run), calibration,
orientation, lesson, end screen. "Pacing" means every pause onset/offset fired at its
scheduled lesson time (150/210, 390/450, 630/690, 840/900 s) and the lesson ended at 900.000.

## Editor and desktop runs (done, 2026-10-13)

No headset attached: the headset conditions ran on the desktop camera with the mouse;
calibration skipped. 18x unless stated (silent frame clock, up to 600 ms lateness).

| # | Build | Cond | Path | Result | Data |
|---|---|---|---|---|---|
| E1 | Editor | 3 | 1 practice round; learner fast / wrong / generic / never; B break at 306 s | Pacing ok; all four pause paths; break froze lesson time (5.85 s); 52 events; `session_end completed` | `checkpoints/cp6/session_c3_completed.csv` |
| E2 | Editor | 1 | 2 practice rounds; B break at 308 s; abort at 507 s; reason participant request | Pacing ok to the abort; `abort`, `withdrawal`, `session_end withdrawn` | `checkpoints/cp6/session_c1_aborted.csv` |
| E3 | Editor | 2 | No practice finished; B break at 304 s; abort at 362 s | `orientation_incomplete done=select missing=press;rotate`; withdrawal logged | `checkpoints/cp6/session_c2_incomplete_orientation.csv` |
| E4 | Editor | 3 | 2 practice rounds; learner slow / late / wrong / fast; B break | Pacing ok; 64 events; pause 1 rotation timeout mid-grab (`grab_end forced=1`), pause 2 prediction timeout then learner rotation (`no_prediction` feedback) | `checkpoints/cp7/editor_c3_slow_late_wrong_fast.csv` |
| E5 | Editor, Launch scene | 3 | Unknown code, empty code, allocated code, manual path, used code | Each refused or confirmed as expected; manual logged `source=manual`; used code refused on relaunch | (console) |
| E6 | Linux player 1x | 1 | Full lesson, 1x, windowed (Checkpoint 5) | 900.0 s, max 29.9 ms late; 33 fps on this laptop (software rendering) | `checkpoints/cp5/desktop_run.txt` |
| E7 | Linux player | 1 | Launch screen, manual entry, Lesson scene, quit | Started at launch; `session_end status=quit` on close | (player data folder) |

## Device runs (to do on the Quest 3 and the study desktop)

Fill in one row per run. Use test codes (e.g. P90 to P99) in a test allocation file, and
pull the data afterwards. Each "Check" is what must be true for the row to pass.

| # | Device | Cond | Path | Check | Result | Notes |
|---|---|---|---|---|---|---|
| D1 | Quest | 3 | Start the app | Launch footer: `display 90 Hz`, battery shown, build version matches `BUILD_STAMP.txt` | | |
| D2 | Quest | 3 | Calibration (hold A), ready, orientation | Sphere and practice cube 0.25 m below eye, 0.8 m ahead, facing you; `calibration plausible=1`; rays select rings; grip turns the cube | | |
| D3 | Quest | 3 | Full lesson, real time, your own answers | Pacing (pull the CSV: pause onsets at 150/390/630/840 s); `[Frames]` 90 Hz with few missed frames; narration in sync | | |
| D4 | Quest | 2 | Full lesson, real time | No rays during the lesson; controllers visible; no prompts or dimming; pacing as D3 | | |
| D5 | Quest | 3 | Take the headset off for ~20 s during a pause, put it back, pull trigger | `hmd_removed` then `hmd_resume`; lesson time frozen across it; narration resumes where it stopped; what reason was logged | | |
| D6 | Quest | 2 | Same as D5 during narration | As D5; record what the Quest did to audio while off | | |
| D7 | Quest | 3 | Press the Meta button mid-lesson, close the menu | Pauses (`hmd_removed reason=focus_lost`) and resumes with the trigger | | |
| D8 | Quest | 3 | Abort with both grips + both thumbsticks 3 s; pick a reason | Ring fills; end screen; `withdrawal` logged | | |
| D9 | Quest | any | `tools/pull_data.sh` after D1-D8 | All files verified, removed from the headset; `session_index.csv` kept | | |
| D10 | Desktop | 1 | Full lesson, fullscreen, real time | `[Display]` 1920x1080 @ 60 Hz with no warning; `[Frames]` 60 fps; cursor hidden in the lesson only; pacing | | |
| D11 | Desktop | 1 | B break, then Esc hold abort | `break_start`/`break_end`; `abort input=esc`; withdrawal reason | | |
| D12 | Labmate | 3 | Naive labmate, full session | Note any timeout (spec: if more than 1 in 5 Cond 3 attempts time out, lengthen all pauses equally); median completion time per pause for the Cond 1/2 arc start | | |
