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
content, pause count, pause length, and total time on task. They differ only in
display, whether the learner manipulates the qubit, prediction prompts, and
feedback. Shared lesson content changes apply to all three conditions. Flag any
feature that would make one condition longer, shorter, or show different
content before building it.

| | Cond 1: 2D | Cond 2: Passive VR | Cond 3: Embodied VR |
|---|---|---|---|
| Display | desktop monitor, 1920x1080, 60 Hz, mouse | Meta Quest 3 | Meta Quest 3 |
| Narration, order, pauses | identical | identical | identical |
| State changes | scripted animation | scripted animation | learner ratchet-rotates the qubit |
| Prediction prompt | none | none | before each pause operation |
| Feedback | none | none | misconception-targeted |

Conditions 2 and 3 are one build with the Cond 3 interaction absent in Cond 2.
Condition 1 is a desktop build of the same lesson.

Known, accepted differences (report in the paper, do not "fix"):
- Cond 1 has a fixed viewpoint; the HMD conditions allow head motion. This is
  part of the display manipulation.
- Cond 1/2 learners watch about 40 s of a static sphere in each pause. Time on
  task stays equal; what differs is what the learner does with that time.

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
the real narration means replacing the clip and editing timestamps only.

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
- Mapping wrong answers to misconceptions lives in an editable data file.

### Prediction format

- Six positions (pauses 2, 3): the learner points the controller ray at one of
  six glowing, labeled markers on the sphere and pulls the trigger.
- P(0) (pauses 1, 4): five buttons on a panel.
- The first answer locks. Answer and response time are logged.

### Pause schedule (times from pause onset, identical every pause)

| Time | Cond 3 | Cond 1 / 2 |
|---|---|---|
| 0 s | sphere dims, prompt appears | nothing |
| 15-18 s | (learner working) | axis appears, scripted arc |
| 25 s | prediction deadline: if unanswered, prompt removed, `prediction_timeout` logged, gate unlocks anyway | |
| 35 s | rotation deadline: remaining rotation plays as a scripted arc (<= 3 s), `rotation_timeout` logged | |
| on completion | feedback shown until 60 s (pauses 1-3) | |
| 40-51 s | pause 4 only: measurement demo, all conditions | same |
| 51-60 s | pause 4 only: feedback (Cond 3) | |
| 60 s | undim, narration resumes | narration resumes |

- Never skip the rotation: the end state must match the other conditions.
- Feedback always appears. With no prediction, show the neutral explanation of
  the correct result (no correct/incorrect framing), logged as feedback type
  `no_prediction`.
- After dry runs give a median Cond 3 completion time, move the Cond 1/2 15 s
  arc start to that time, then freeze.
- Cond 1/2 show nothing else in the pause: no dimming, no prompt, no cues.

### Gate press, direction and tolerance (Cond 3)

- After the prediction locks, one button appears, labeled with the operation
  ("H", "Quarter turn about z"). Pressing it shows the axis and direction
  arrow and unlocks the grab. This is the `gate_press` event. Cond 1/2 have no
  button at all (gate presses are 0 by design); the axis just appears when the
  scripted arc starts.
- Only the positive (right-hand rule) direction counts. The grab is a ratchet:
  it turns one way about the axis and stops at the target angle (no overshoot).
  A curved arrow shows the direction; a light haptic tick fires on pushes the
  wrong way. This makes the Cond 3 arc identical to the scripted arc.
- The gate completes when the learner releases within +/-10 deg of the target;
  the vector then eases to the exact target state. Widen to +/-15 deg only if
  dry runs show it is fiddly.
- Grab works up close and by ray (distance grab) so the seated learner never
  leans or stands. The grab start event logs which mode was used.

### Measurement demo

- Scripted and identical in all three conditions at fixed times; the learner
  never triggers shots. Used at segment 4 (on |+>) and pause 4 (on Ry(pi/3)|0>).
- 100 shots, fixed seed per demo. Seeds are pre-checked so the result lands
  close to the true probability (about 74/26 for pause 4, about 50/50 for |+>).
- First 5 shots get the full collapse at 1 Hz: flash about 150 ms, snap to the
  pole, hold, fade out, fade back in at the prepared state. The remaining 95
  fill the histogram over about 4 s with no flash and no vector motion. A
  counter shows the shots running.
- A dim, constant "ghost" vector stays at the prepared state for the whole
  demo, so the fade-in reads as "prepared again".

### Design requirements from the paper

- Gate rotations animate as continuous arcs about the gate's rotation axis
  (H is a pi rotation about (x+z)/sqrt(2)). Interpolate the axis-angle
  rotation; never lerp between end points.
- State vector length is fixed at 1; show polar and azimuthal angles distinctly;
  measurement is visually a collapse, not a turn.

## Visual design

The Bloch sphere is the whole show. Tron-like: glowing structures and neon
lines against darkness. Identical in all conditions except Cond 3 pause
dimming (and the Cond 3 prompt/feedback panels themselves).

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
- Cond 3 pause dimming: sphere emission to about 30 %, state vector and active
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
- No flashing faster than 3 Hz, no full-field strobes. Measurement flash is a
  single short pulse localized to the sphere.
- Cond 3 feedback: cyan for correct, amber for incorrect, always with text.

## Condition 3 interaction (embodied)

At each pause: sphere dims -> prediction prompt -> gate press -> ratchet
rotation about the gate axis -> feedback chosen by which wrong answer was
picked (misconception mapping in a data file). Controllers only, no hand
tracking. Deadlines and timeout behavior as in the pause schedule above.

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
  center the sphere, 60 deg vertical FOV. Cursor hidden during the lesson; the
  mouse is used only in orientation and on the end screen.
- Then the lesson, then an end screen telling the participant to remove the
  headset (or look away from the monitor) and see the experimenter. The
  experimenter end screen shows the data file name and event count.
- Data: `persistentDataPath`, file name like `P07_C3_20261020-1432.csv`, never
  overwritten. A `pull_data` script (sh and ps1) runs `adb pull`, verifies the
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

Bloch +z is up (Unity +Y), Bloch +x points toward the participant (Unity -Z,
since the participant faces +Z), Bloch +y points to the participant's right
(Unity +X). This is the usual textbook drawing. The conversion lives in one
place (`BlochSpace`).

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
