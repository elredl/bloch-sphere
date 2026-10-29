# Study Spec: VR Quantum Computing Instruction Study (Unity)

Living spec for the three-condition between-subjects study build. It is the
original build prompt with every Checkpoint 0 decision folded in. If code and
this file disagree, this file wins until it is changed on purpose.

Frozen engine: **Unity 6000.6.0f1** (Unity 6.6). This replaces the "6.3 LTS"
in the original prompt (decided at Checkpoint 0, 2026-10-12). Do not upgrade
Unity, change the render pipeline (URP 17.6), or change packages without asking.

## How we work

- Work in checkpoints. Stop at the end of each one, report (what was built,
  files touched, how it was verified, open doubts) and wait for approval.
- Ask about anything ambiguous that affects what participants see, hear, or do.
- One commit per checkpoint on `study-build`, named after the checkpoint.
- Verify in the running editor via the live command interface
  (`Assets/Editor/LiveCommandRunner.cs`). Never report something as working
  without running it.
- Small components, clear names, short comments explaining *why*.

## The experimental invariant (most important rule)

The three conditions are identical in narration audio, segment order, segment
content, pause count, and maximum pause length. Pauses are learner-paced (see
Next button below), so actual pause length and total time on task vary per
learner and are logged; the Next button itself is the same in every condition.
They differ only in
display and whether the learner manipulates the qubit. Prediction prompts,
feedback and pause dimming are identical in all three conditions (decided
2026-10-29, so that Cond 2 vs Cond 3 isolates the hand interaction instead of
bundling it with prediction and feedback). Shared lesson content changes apply
to all three conditions. Flag any
feature that would make one condition longer, shorter, or show different
content before building it.

| | Cond 1: 2D | Cond 2: Passive VR | Cond 3: Embodied VR |
|---|---|---|---|
| Display | desktop monitor, 1920x1080, 60 Hz, mouse | Meta Quest 3 | Meta Quest 3 |
| Narration, order, pauses | identical | identical | identical |
| Pause operation | scripted arc after the prediction | scripted arc after the prediction | learner ratchet-rotates the qubit |
| Prediction prompt | same, mouse | same, controller ray | same, controller ray |
| Feedback | misconception-targeted | misconception-targeted | misconception-targeted |

Conditions 2 and 3 are one build with the Cond 3 interaction absent in Cond 2.
Condition 1 is a desktop build of the same lesson.

Known, accepted differences (report in the paper, do not "fix"):
- Cond 1 has a fixed viewpoint; the HMD conditions allow head motion. This is
  part of the display manipulation.
- Cond 1/2 learners watch the turn (about 3 s) where Cond 3 learners make it
  (gate press and grab), so the pause is busier in Cond 3 and pause length
  under Next may differ by condition (`pause_skipped` records it).

## Lesson content

One narrated lesson, about 15 minutes, four segments in order:
1. Superposition
2. The Bloch sphere (narration must introduce "rotation about an axis")
3. Single-qubit gates
4. Measurement

Each segment ends in a **60 s** pause (four pauses, 4 min total). The narration
is one audio file with the pauses built in as silences. Until it exists, a
placeholder clip is used and everything is driven by a data-driven timeline
(JSON) listing segment and pause times and the scripted events. Swapping in
the real narration means replacing the clip and editing timestamps only. The draft
script, timed against the timeline and checked by `tools/narration_script.py`, is
`docs/NARRATION_SCRIPT.md`.

Pause length tuning: run internal dry runs with labmates first. If more than
about 1 in 5 Cond 3 attempts time out, lengthen all four pauses by the same
amount, then freeze.

### Gates

- Named gates, taught with scripted arcs in segment 3, in this order:
  X on |0> -> |1>, Z on |+> -> |->, H on |0> -> |+>.
- Unnamed rotations, described in words: Ry(pi/2), Rz(pi/2), Ry(pi/3) at
  pauses 1, 2 and 4.
- Y, S and T are not taught (keeps load down). The model and unit tests still
  cover them.

### Pause tasks

| Pause | Start state | Operation | Prediction | Format | Wrong answers -> misconception |
|---|---|---|---|---|---|
| 1 (after Superposition) | \|0> | quarter turn toward \|1>, Ry(pi/2) -> \|+> | how often a measurement gives 0 | P(0): 100 / 75 / 50 / 25 / 0 % | rotation doesn't change the outcome (100 %); quarter turn reaches \|1> (0 %) |
| 2 (after Bloch sphere) | \|+> | quarter turn about z, Rz(pi/2) -> \|+i> | where the vector ends up | six positions | wrong rotation direction (\|-i>); polar vs azimuthal confusion (\|0> or \|1>); z rotation does nothing here (stays \|+>) |
| 3 (after Gates) | \|+> | H -> \|0> | where the vector ends up | six positions | H always creates superposition (stays \|+>); superposition can't become definite again; confusing H with X (\|1>) |
| 4 (after Measurement) | \|0> | 60 deg turn toward \|1>, Ry(pi/3) | P(0) of the result (75 %), then the measurement demo plays | P(0): 100 / 75 / 67 / 50 / 25 % | any superposition is 50/50 (50 %); probability falls linearly with angle (67 %); only the equator counts as superposition (100 %) |

- Pause 1 is deliberately the easiest; it is also the first use of the interaction.
- Pause 3 starts from the end of segment 3's scripted H on |0>, so the learner
  sees H undo itself (interference).
- The timeline sets each pause's start state explicitly. At pause onset the app
  checks the live state against it and logs an error if they differ.
- Mapping wrong answers to misconceptions lives in an editable data file:
  `Assets/Study/Data/pause_feedback.json` (prompt text, correct text, neutral
  `noPrediction` text, one text per misconception with the answers that map to
  it, and a `genericIncorrect` text). Wrong answers with no mapped misconception
  get the generic text, logged as feedback type `generic_incorrect` (decided at
  Checkpoint 3 review): pause 1 answers 75 % and 25 %, pause 2 answer |->,
  pause 4 answer 25 %. All wording is a draft until reviewed.

### Prediction format

- Six positions (pauses 2, 3): the learner points the controller ray at one of
  six glowing, labeled markers on the sphere and pulls the trigger (Cond 1:
  clicks one with the mouse). Each marker
  is a ring in its axis color with its own ket label at full brightness; they
  appear only while the prompt is open.
- P(0) (pauses 1, 4): five buttons on a panel.
- The first answer locks. Answer and response time are logged.
- Controls (XRI defaults): trigger picks answers and presses buttons, grip
  grabs the vector. Orientation (Checkpoint 6) practises both.
- The prompt and feedback share one panel, upper left of the sphere (the
  histogram's mirror image), 240 x 270 mm. Title PREDICT while the prompt is
  open, TURN from the gate press (or the prediction timeout; Cond 1/2: while
  the arc plays, "Watch the turn."), then CORRECT / NOT QUITE / RESULT for
  feedback.

### Pause schedule (times from pause onset, identical every pause)

| Time | Cond 3 | Cond 1 / 2 |
|---|---|---|
| 0 s | sphere dims, prompt appears | same |
| answer | answer locks, gate button appears | answer locks; 1 s later axis appears, scripted arc (3 s) |
| 25 s | prediction deadline: if unanswered, prompt removed, `prediction_timeout` logged, gate unlocks anyway | same; scripted arc at 26 s |
| 35 s | rotation deadline: remaining rotation plays as a scripted arc (<= 3 s), `rotation_timeout` logged | |
| on completion | feedback shown until 60 s (pauses 1-3); pause 4 hides the panel until its feedback | same |
| 40-51 s | pause 4 only: measurement demo | same |
| 51-60 s | pause 4 only: feedback | same |
| 60 s | undim, narration resumes | same |

- Never skip the rotation: the end state must match the other conditions.
- Feedback always appears. With no prediction, show the neutral explanation of
  the correct result (no correct/incorrect framing), logged as feedback type
  `no_prediction`.
- Cond 1/2 show nothing beyond Cond 3's prompt, feedback and dimming: no gate
  button, no direction arrow, no grab.

### Next button (all conditions)

- A NEXT button to the right of the sphere, below the measurement panel. It
  appears once the prediction is over (answer locked, or the 25 s deadline
  passed), so the question can't be skipped, and stays until the pause ends. Controller ray in the headset (Cond 2 gets its
  rays only during pauses), mouse on the desktop (the cursor shows only during
  pauses).
- Pressing it ends the pause at once: the narration jumps to the pause's end
  and the next segment starts. Whatever in the pause has not happened yet is
  skipped (the turn, feedback, pause 4 measurement demo); the
  pause operation snaps to its result so the end state is still the same.
- Logged as `pause_skipped` (pause, seconds into the pause, seconds skipped);
  `lesson_end` carries the total skipped as `skipped_s`.

### Gate press, direction and tolerance (Cond 3)

- After the prediction locks, one button appears, labeled with the operation
  ("H", "Quarter turn about z"). Pressing it shows the axis and direction
  arrow and unlocks the grab. This is the `gate_press` event. Cond 1/2 have no
  button at all (gate presses are 0 by design); the axis just appears when the
  scripted arc starts, 1 s after the prediction is over.
- Only the positive (right-hand rule) direction counts. The grab is a ratchet:
  it turns one way about the axis and stops at the target angle (no overshoot).
  A curved arrow shows the direction; a light haptic tick fires on pushes the
  wrong way. This makes the Cond 3 arc identical to the scripted arc.
- Ratchet behavior (Checkpoint 4): the grab point's angle about the gate axis
  drives the vector. The vector follows the hand forward, stays put while the
  hand goes back (one tick per 10 deg pulled back), and resumes once the hand
  passes it again. A grab never makes the vector jump; it is measured from
  where the hand was when the grab started. Releasing early leaves the vector
  where it is; the learner can grab again.
- The arrow is a short amber arc just outside the sphere from the tip along
  the path (about 36 deg ahead, never past the target), so it shows the way
  without showing the end point. It shrinks away as the target is reached.
- After a release within tolerance the last few degrees ease in at the
  scripted arc's speed. The timeout arc plays the remaining fraction of the
  turn in `timeoutArcMaxDuration` x that fraction (3 s for a whole turn).
- The gate completes when the learner releases within +/-10 deg of the target;
  the vector then eases to the exact target state. Widen to +/-15 deg only if
  dry runs show it is fiddly.
- Grab works up close and by ray (distance grab) so the seated learner never
  leans or stands. The grab start event logs which mode was used. The grab
  point is fixed to the controller like the end of a stick: with a ray it sits
  out at the sphere, so pointing moves it. A grab that starts with the
  controller within 15 cm of the tip is logged as `near`, otherwise `ray`.
- The grab handle exists in the shared Cond 2/3 build but only switches on in
  Cond 3, and only between the gate press and the end of the rotation.
- Cond 2 learners see their controllers (same as Cond 3); their pointer rays
  switch on only during pauses, for the prediction and the Next button.

### Measurement demo

- Scripted and identical in all three conditions at fixed times; the learner
  never triggers shots. Used at segment 4 (on |+>) and pause 4 (on Ry(pi/3)|0>).
- 100 shots, fixed seed per demo. Seeds are pre-checked so the result lands
  close to the true probability. Chosen at Checkpoint 3: seed 27 gives exactly
  50/50 on |+>, seed 57 gives exactly 75/25 on Ry(pi/3)|0> (true value 75/25).
- First 5 shots get the full collapse at 1 Hz: flash about 150 ms, snap to the
  pole, hold, fade out, fade back in at the prepared state. The remaining 95
  fill the histogram over about 4 s with no flash and no vector motion. A
  counter shows the shots running.
- A dim, constant "ghost" vector stays at the prepared state for the whole
  demo, so the fade-in reads as "prepared again".
- The histogram panel sits upper right of the sphere (196 x 180 mm, inside the
  Cond 1 frame), shows the shot counter and per-outcome counts with
  percentages, and hides when the narration moves on (t = 820 s) or at the end
  of pause 4.

### State changes that are not gates

- `set_state` (re-preparing the qubit, e.g. back to |0> before a new gate)
  fades the vector out, changes the state while it is invisible and fades it
  back in, 0.6 s in total. It never animates as a rotation and leaves no
  trail, so it cannot be mistaken for a gate.

### Design requirements from the paper

- Gate rotations animate as continuous arcs about the gate's rotation axis
  (H is a pi rotation about (x+z)/sqrt(2)). Interpolate the axis-angle
  rotation; never lerp between end points.
- State vector length is fixed at 1; show polar and azimuthal angles distinctly;
  measurement is visually a collapse, not a turn.

## Visual design

The Bloch sphere is the whole show. Tron-like: glowing structures and neon
lines against darkness. Identical in all conditions, pause dimming and the
prompt/feedback panel included; only Cond 3 has the direction arrow.

### Style tokens

Every color, glow intensity, line width and font lives in one `StylePalette`
ScriptableObject. No hard-coded colors elsewhere.

| Element | Color | Notes |
|---|---|---|
| Background | near-black `#05060A` | not pure black (Quest 3 LCD) |
| Sphere wireframe | dim cyan `#00F0FF`, low emission | quiet, structural |
| Equator and prime meridian | cyan `#00F0FF`, higher emission | reference rings |
| Sphere shell | very faint translucent cyan | reads as a volume |
| X axis | orange `#FF9E1B` | |
| Y axis | violet `#A970FF` | |
| Z axis | cyan `#00F0FF`, brightest axis | \|0> and \|1> labels at the poles |
| State vector | white-hot core, magenta `#FF2E88` glow | brightest object, tip marker |
| Gate rotation axis | dashed glowing amber `#FFE14D` | only while a gate is applied |
| Gate arc trail | amber `#FFE14D`, fading tail | |
| Polar angle (theta) arc | z cyan | |
| Azimuthal angle (phi) arc | x orange | |
| Measurement histogram | \|0> cyan, \|1> magenta | |

Color always tells you which axis or quantity an element belongs to. Never
red vs green; never color alone (pair with labels or shape).

### Sphere styling

- Crisp glowing instrument: thin grid, brighter reference rings, bright axes,
  dominant vector.
- Lines are thin tube meshes or anti-aliased line shaders, not 1-pixel
  LineRenderers. MSAA 4x in the VR build.
- Vector length always equals the sphere radius; glow, thickness and brightness
  never vary with probability.
- Measurement: a single short collapse flash and a snap to a pole, no trail.
  Rotation always has the trail.
- Pause dimming (all conditions): sphere emission to about 30 %, state vector and active
  gate axis stay at full brightness.
- Size: 0.6 m diameter. Placement is calibrated per participant (see Session
  flow): center 0.25 m below eye level, 0.8 m forward. Same framing in all
  conditions; the Cond 1 camera matches the calibrated VR eye point.

### Environment

- Darkness, no skybox detail, no props.
- Dim Tron grid floor **at the real floor level** from Quest floor tracking
  (about 0.9-1.0 m below the sphere when seated), fading with distance.
- Completely static. Only the qubit visuals move.

### UI and text

- Neon on frames and accents only. Body text off-white on a dark translucent
  panel, no glow on letters.
- Inter for reading text; Orbitron allowed for short titles only.
- TextMeshPro world-space panels below or beside the sphere, never covering
  it. Size confirmed on Quest 3.

### Glow, performance, comfort

- Quest 3 holds 90 Hz. Glow from emissive unlit materials and fresnel/halo
  shaders. If bloom is used, measure frame rate with and without and report both.
  Bloom is not used (Checkpoint 3): the halo tubes give the glow, post-processing
  is off on every camera and the Quest renderer has none, so the device check
  is one build, `Builds/Quest/lesson_nobloom.apk`, read from `[Frames]` log lines.
- No flashing faster than 3 Hz, no full-field strobes. Measurement flash is a
  single short pulse localized to the sphere.
- Feedback (all conditions): cyan for correct, amber for incorrect, always with text.

## Condition 3 interaction (embodied)

At each pause: sphere dims -> prediction prompt -> gate press -> ratchet
rotation about the gate axis -> feedback chosen by which wrong answer was
picked (misconception mapping in a data file). Controllers only, no hand
tracking. Deadlines and timeout behavior as in the pause schedule above.
Implemented in `EmbodiedPause` (flow), `RatchetRotation` (grab math, unit
tested), `QubitGrabTarget` (XR grab), `PredictionPanelView`,
`PositionMarkersView` and `GateDirectionView`.

## Telemetry

Event-driven only. No continuous head or hand tracking. One row per event:
timestamp, participant code, condition, stratum, event type, event fields.

Events: session start/end; orientation step completed, `orientation_incomplete`;
pause onset/offset; pause start-state mismatch (error); grab start (with grab
mode: near or ray) / grab end; gate press; prediction response (answer,
response time); `prediction_timeout`; `rotation_timeout`; feedback shown
(misconception id or `no_prediction`); measurement demo; break start/end
(with duration); `hmd_removed` / resume; withdrawal (with reason code);
achieved frame rate (periodic summary, a check, not tracking).

CSV, one file per session, flushed after every event. No names or identifying
information, only codes like P07.

Since Checkpoint 4 the pause events (all conditions; gate and grab events Cond 3 only) go through `StudyEvents.Log` and
appear as `[Event]` log lines: `prediction_response` (answer, rt from pause
onset), `prediction_timeout`, `gate_press`, `grab_start` (mode),
`grab_end` (angle, target, complete, forced on timeout), `rotation_complete`,
`rotation_timeout` (gate_pressed, remaining degrees), `feedback_shown` (type:
correct, misconception, generic_incorrect or no_prediction; id).

Checkpoint 6 (`StudySession`, `SessionLog`): every `StudyEvents.Log` call goes to
the session CSV, columns `utc,session_s,lesson_s,participant,condition,stratum,event,fields`
with `app_version` after `stratum` (`lesson_s` blank outside the lesson; `fields` is
`key=value key=value`). Rows are
flushed to disk one by one; the file is created with CreateNew, so it is never
overwritten. Session events: `session_start` (source allocation/manual, app and
device), `calibration` (eye height, yaw corrected), `orientation_start`,
`orientation_step` (select, press, rotate; round), `orientation_grab`,
`orientation_incomplete`, `orientation_end`, `lesson_start`, `segment_start`,
`pause_onset`, `pause_offset`, `pause_skipped`, `pause_state_mismatch`, `measurement_demo`,
`lesson_end`, `break_start` / `break_end` (duration), `hmd_removed` (reason
app_paused, focus_lost or user_absent; head offset from the calibrated eye) /
`hmd_returned` (head offset) / `hmd_resume` (duration), `tracking_origin_changed`,
`display_rate` (Quest refresh rate after the 90 Hz request),
`focus_lost` / `focus_back` (desktop), `abort` (step, input), `withdrawal`
(reason), `frame_rate` (every 60 s), `session_end` (status, events).

## Session flow and experimenter tools

- Experimenter launch screen takes the participant code. Condition and stratum
  come from `allocation.csv` with columns `participant_code,condition,stratum`
  (condition 1/2/3; stratum `math_literate` or `novice`). The app refuses
  codes not in the file and codes that already have a session file. Manual
  condition entry only as a fallback with a confirmation step. The participant
  never sees the condition name.
- Seated, on a chair that doesn't swivel or roll.
- Calibration at the start of orientation: participant sits upright facing
  forward, experimenter presses recenter, the app records eye height and places
  the sphere center 0.25 m below eye level and 0.8 m forward.
- Orientation: fixed 2:00 in every condition, same neon environment, same
  guided practice sequence: ratchet-rotate a glowing cube about a fixed axis,
  ray-select one of several targets, press a button. Cond 2 practices grabbing
  too (matched exposure with Cond 3). Cond 1 does the same with the mouse.
  Completed steps are logged. Early finishers get free practice until 2:00;
  unfinished is logged as `orientation_incomplete`, never extended.
- Cond 1 camera: fixed at the calibrated-equivalent eye point, pitched to
  center the sphere, 60 deg vertical FOV. Cursor hidden while the narration
  plays; the mouse is used in orientation, for the Next button in pauses, and
  on the end screen.
- Cond 1 display (Checkpoint 5, `DesktopDisplay`): 1920 x 1080 borderless
  fullscreen, vsync on (one frame per refresh at 60 Hz), not resizable,
  cursor hidden and locked from lesson start to lesson end. The app logs a
  `[Display]` line with what the monitor actually gave and warns if it is not
  1920 x 1080 at 60 Hz; `[Frames]` lines report the frame rate as in the
  headset. XR never starts on desktop: the desktop target has no XR loader and
  XR start-up is off (Quest keeps OpenXR). Build: `StudyBuild.BuildDesktop`.
  Only Linux desktop support is installed; a Windows build uses the same
  settings once that module is added. Dev launch flags `-dev-windowed` and
  `-dev-mute` exist for dry runs only.
- Implementation (Checkpoint 6): `LaunchScreen` (Launch scene, build index 0),
  `SessionFlow`, `Orientation`, `SessionInput`, `EndScreen` (Lesson scene).
  Participant wording is in `Data/session_text.json`; `tools/export_participant_text.py`
  compiles all participant-facing text into `docs/PARTICIPANT_TEXT.md`. The launch screen
  lists the unused codes for this device and shows the code only, never the condition;
  codes are P plus exactly two digits (P01 to P99), also in allocation.csv; manual
  entry (fallback only) refuses codes that are in allocation.csv. The data folder holds `allocation.csv`, the session files
  and `session_index.csv` (every session started on the device; `pull_data`
  leaves it, so a code can't be reused later either). In the Editor the data
  folder is `DevData/` in the project. Calibration on Quest: the experimenter holds
  A (right controller) 1 s while the participant sits upright facing forward; the pose is
  recorded once the head has been still for 1 s (within 1.5 cm and 3 deg; after 10 s it
  is taken anyway and logged still=0). A does nothing except here and on the headset
  resume screen, where it recalibrates. Head position relative to the calibrated eye is
  logged at removal and remount, and `tracking_origin_changed` when the runtime moves the
  origin. Then "pull the trigger when ready" starts orientation. Orientation practises the
  steps in lesson order (select, press, rotate). The cube turns 90 deg about a
  vertical axis with the lesson's ratchet and tolerance.
- Then the lesson, then an end screen telling the participant to remove the
  headset (or look away from the monitor) and see the experimenter. The
  experimenter end screen shows the data file name and event count.
- Data: `persistentDataPath`, file name like `P07_C3_20261020-1432.csv`, never
  overwritten. `tools/push_allocation.sh|ps1` copies allocation.csv to the Quest.
  A `pull_data` script (`tools/pull_data.sh|ps1`) runs `adb pull`, verifies the
  file count, then deletes the files from the headset, at the end of each
  session day.
- Breaks and abort:
  - Desktop: hold Esc 2 s to abort; B pauses/resumes (break, logged with duration).
  - Quest: taking the headset off pauses timeline and narration and logs
    `hmd_removed`; on remount a "pull trigger to continue" screen appears and
    resume is logged. Verify on device what Quest does to audio and focus on
    removal, since the timeline runs off audio time.
  - Quest abort: hold both grips and click both thumbsticks for 3 s, visible
    progress ring, no confirmation.
  - Abort logs a withdrawal with a reason code entered on the end screen:
    discomfort, participant request, or equipment failure.
  - Breaks are reported in the paper with withdrawals and protocol deviations,
    by condition.

Questionnaires and tests happen on a tablet outside the app.

## Coordinate convention

Bloch +z is up (Unity +Y). In the sphere's own frame, Bloch +x points toward
the participant and Bloch +y to their right; the conversion lives in one place
(`BlochSpace`). The whole sphere is then turned 30 deg about the vertical so +x
points toward the participant's front-left and +y to the right and slightly
toward them: the textbook oblique view. Without the turn, x points straight at
the viewer and collapses to a dot, and the prime meridian hides behind the z
axis (decided at Checkpoint 1.5, 2026-10-12). Same turn in all conditions.

## Checkpoints

0. Audit and plan. Done 2026-10-12.
1. Quantum state model and arc animation, Edit Mode tests, existing sphere
   reads from the model. Screenshot of an arc mid-animation.
1.5. Visual style lock: `StylePalette`, restyled sphere, environment, sample UI
   panel; four states (idle |0>, mid arc, theta/phi arcs, Cond 3 dimmed);
   Quest frame rate with and without bloom.
2. Lesson timeline and pacing: timeline data, audio-clock-driven lesson
   controller, condition config; durations identical across conditions.
3. Condition 2 (passive VR): full lesson in headset, scripted arcs, measurement
   demo, Cond 3 interaction absent.
4. Condition 3 (embodied VR): dimming, prediction, gate press, ratchet grab,
   tolerance, feedback, timeouts; slow runs still end every pause on time.
5. Condition 1 (2D desktop): 1920x1080, mouse, fixed camera; timing matches Cond 2.
6. Telemetry, session flow, calibration, orientation, breaks, abort, pull_data.
7. Pilot hardening: 90 Hz at startup, builds, `BUILD_INFO.md`,
   `EXPERIMENTER_CHECKLIST.md`, end-to-end dry run table.
   Done 2026-10-13: `DisplayRate` requests 90 Hz and the launch screen shows the actual rate,
   battery and build version; builds carry `1.0+<commit>` (`BUILD_STAMP.txt`); docs in
   `docs/BUILD_INFO.md`, `docs/EXPERIMENTER_CHECKLIST.md`, `docs/DRY_RUNS.md` (device rows
   still to run).
