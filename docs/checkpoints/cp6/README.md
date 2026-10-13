# Checkpoint 6: telemetry and session flow

Editor dry runs at 18x on 2026-10-13. No headset was attached, so the headset
conditions ran on the desktop camera with mouse input; calibration was skipped.

| File | What it shows |
|---|---|
| `a_launch_entry.png` | Launch screen: keypad, allocation status, data folder |
| `b_launch_confirm.png` | Allocated code confirmation (P01: condition 3, novice) |
| `c_launch_manual.png` | Manual fallback, stratum step |
| `d_ready.png` | "Click when you are ready to begin" (desktop wording) |
| `e_practice_select.png` | Orientation, select step: one lit ring of five |
| `f_practice_rotate.png` | Orientation, rotate step: cube, axis, handle, arrow (mid-turn) |
| `g_break.png` | B-key break overlay over the lesson |
| `h_end_participant.png` | End screen, participant view |
| `i_end_withdrawal_reason.png` | End screen after an abort: withdrawal reason |
| `j_end_experimenter.png` | End screen after a completed lesson: file and event count |
| `session_c3_completed.csv` | C3, one practice round, simulated learner (fast, wrong, generic, never), break at lesson 306 s |
| `session_c1_aborted.csv` | C1, two practice rounds, break at 308 s, abort at 507 s, reason participant_request |
| `session_c2_incomplete_orientation.csv` | C2, orientation not finished (`orientation_incomplete`), abort at 362 s |

Results:

- Every pause onset and offset was at its scheduled time in all three runs; the C3
  run ended at lesson time 900.000 with max lateness 600 ms (the 18x frame clock).
- Breaks freeze lesson time: `break_start` and `break_end` share the same `lesson_s`.
- Launch screen refused an unknown code, an empty code, and a code that already had a
  session; the manual path logged `source=manual`.
- The built Linux player (`StudyBuild.BuildDesktop`) started at the launch screen and
  went through manual entry to the Lesson scene, then quit with `session_end status=quit`.
- Not verified here (needs the Quest): calibration, trigger and A-button input, the
  two-grip + thumbstick abort, headset-off detection and what the Quest does to audio.
