# Experimenter checklist

The participant never hears or sees the condition name. Only the experimenter sees it,
on the launch screen, before the participant puts on the headset or sits down.

## Once, before the first session day

- [ ] Install the current builds (see `BUILD_INFO.md`); note the version from `BUILD_STAMP.txt`.
- [ ] Quest: copy the allocation with `tools/push_allocation.sh allocation.csv`
      (Windows: `tools\push_allocation.ps1`). Start the app once first if the push fails.
- [ ] Desktop: copy `allocation.csv` into the data folder shown at the bottom of the launch screen.
- [ ] The launch screen footer says `allocation.csv: N codes` (not "not found" or "REJECTED").

## Each session day, before the first participant

Quest:
- [ ] Charged (launch screen footer shows the battery; red below 40 %).
- [ ] Boundary: stationary (seated) boundary, set at the participant chair.
- [ ] Floor level correct (Quest settings > Boundary); a wrong floor shows as an implausible
      eye height in the session file (`calibration plausible=0`).
- [ ] Volume at the lab's fixed level; lenses clean; face cover clean.
- [ ] Launch screen footer shows `display 90 Hz` (red if not). If not 90 Hz: restart the app;
      if still not, set 90 Hz in Quest settings > Display and restart.

Desktop:
- [ ] Monitor 1920 x 1080 at 60 Hz. The first line of `Player.log` with `[Display]` must have no warning.
- [ ] Headphones or speakers at the lab's fixed level.
- [ ] Mouse on the desk; no other windows open; notifications off.

Room: non-swivel, non-rolling chair; same chair position for every participant.

## Each session

1. Start the app. The launch screen appears.
2. Type the participant number (P is fixed; 07 for P07) and press START.
   - Refused codes: not in allocation.csv, already used on this device, or allocated to the
     other device (Quest runs conditions 2 and 3; desktop runs 1). Check the code; do not
     use MANUAL to get past a refusal unless the protocol says so.
3. Check the confirmation (code, condition, stratum) and press CONFIRM.
4. **Quest**: put the headset on the participant (still holding the controllers yourself).
   Ask them to sit upright and look straight ahead, then hold **A** on the right controller
   for 1 second. Hand over the controllers.
   **Desktop**: seat the participant at the monitor.
5. The participant pulls the trigger (desktop: clicks) when ready. Orientation runs exactly
   2:00, then the lesson (15:00) starts by itself. Do not talk during the lesson.
6. At the end screen the participant takes off the headset (or looks away) and tells you.
7. Press **Experimenter** on the end screen. Note the file name and event count on the
   session sheet. Press **Quit**.

## Breaks and stopping

- Desktop break: press **B** to pause, **B** again to resume.
- Quest break: the participant takes off the headset; the lesson pauses. When it is back on,
  the participant pulls the trigger to continue.
- Stop the session (withdrawal): Quest: hold **both grips and click in both thumbsticks** for
  3 seconds (ring fills). Desktop: hold **Esc** for 2 seconds. On the end screen press
  **Experimenter** and pick the reason: discomfort, participant request or equipment failure.
- Write every break and stop on the session sheet (time and reason). They are reported in the
  paper by condition.

## If something goes wrong

- App crashed or was closed mid-session: the session file up to that point is kept and the code
  is now refused. Record it as a protocol deviation; follow the protocol for whether and how the
  participant is re-run (the app will not reuse the code).
- Participant pressed the Quest Meta button: the lesson should pause as if the headset came
  off; close the menu and the participant pulls the trigger to continue. (Confirm in the
  device dry run, row D7 of `DRY_RUNS.md`.)
- Wrong person in the headset at the launch screen: nothing has been logged until CONFIRM.

## End of each session day

- [ ] Quest connected by USB, USB debugging allowed in the headset.
- [ ] `tools/pull_data.sh` (Windows: `tools\pull_data.ps1`). It copies the session files to
      `study_data/<date-time>/`, checks every file's size, and only then deletes them from the
      headset. `session_index.csv` is copied and stays on the headset.
- [ ] The script says `verified N files`; N matches the session sheet.
- [ ] Desktop: copy the `P*_C1_*.csv` files from the desktop data folder to the same archive.
- [ ] Back up the archive.
