# Editing the lesson timeline

The whole lesson is driven by `Assets/Study/Data/lesson_timeline.json`.
All times are seconds on the narration clip. The same file drives all three
conditions; only what happens inside a pause differs by condition.

## Swapping in the real narration

1. Import the recording into `Assets/Study/Audio/` (any format Unity reads).
2. Select the `Lesson` object in `Assets/Study/Scenes/Lesson.unity` and drag the
   clip into **Narration**. (Empty = generated placeholder beeps.)
3. Set `lessonDuration` to the clip length and edit the segment and event
   times to match the recording. The clip and `lessonDuration` must agree to
   within 0.5 s or the lesson refuses to start.
4. Run the Edit Mode tests (`Study.Tests.EditMode`). They check every rule below.

## Rules the app enforces (the lesson will not start otherwise)

- Exactly 4 segments, back to back: each `start` equals the previous `pauseEnd`.
- Every pause (`pauseEnd - pauseStart`) equals `pauseSchedule.pauseLength`.
- The last `pauseEnd` equals `lessonDuration`.
- No scripted event, and no arc or measurement demo, overlaps a pause. Pauses
  belong to the pause behavior.
- Arcs and demos don't overlap each other.
- The scripted events must leave the qubit in each pause task's `startState`
  at that pause's onset. The app checks this at load time and again live at
  each pause onset (an error is logged if they differ).

## Segments

```json
{ "id": "gates", "start": 450.0, "pauseStart": 630.0, "pauseEnd": 690.0, "task": "pause3" }
```
Narration runs `start` to `pauseStart`; the silent pause runs `pauseStart` to `pauseEnd`.

## Events

| type | fields | what it does |
|---|---|---|
| `set_state` | `state` | re-prepares the qubit (`0`, `1`, `+`, `-`, `+i`, `-i`) |
| `apply_gate` | `gate`, `duration` | arc animation; gates `X Y Z H S T`, `Rx(deg)`, `Ry(deg)`, `Rz(deg)` |
| `show` / `hide` | `target` | `angle_arcs` (theta and phi arcs) |
| `measure_demo` | `state`, `shots`, `seed` | repeated-measurement panel |

Arcs start at their scheduled time even if a frame runs late, so they never
drift from the narration.

## Pause schedule (seconds from pause onset, same for every pause)

| field | meaning |
|---|---|
| `arcDuration` | Cond 1/2: length of the scripted arc, which starts 1 s (`EmbodiedPause.WatchDelay`) after the prediction locks or times out |
| `predictionDeadline` | All: unanswered prompt removed (Cond 3: gate unlocks; Cond 1/2: arc plays) |
| `rotationDeadline`, `timeoutArcMaxDuration` | Cond 3: the remaining rotation plays as a scripted arc |
| `measurementDemoStart`, `measurementDemoEnd` | pause 4 demo, all conditions |

## Prompts and feedback (all conditions)

The prompt, feedback and panel text live in
`Assets/Study/Data/pause_feedback.json`, one entry per pause task (matched by
`id`). Answers use the same strings as the task's `choices`. Each wrong answer
can appear in at most one misconception; unmapped wrong answers get
`genericIncorrect`. The Edit Mode test `RealFeedback_IsValidAgainstTheTimeline`
checks the file against the timeline.

## Dry runs in the Editor

`LessonDevTools.SetDevRun("condition,speed")` sets the dev condition and
speed. Up to 3x the narration plays faster; above 3x (up to 18x) the run is
silent and lesson time runs off frame time, capped per frame so hitches slow
the run instead of skipping time. In Condition 3,
`LessonDevTools.SimulateLearner("fast,wrong,slow,never")` plays a learner
through the four pauses (profiles in `SimulatedLearner.Named`).

## Measurement seeds

A demo's `seed` fixes its shot sequence, identical for every participant and
on every platform. The tests require each seed to land on the true
probability over all shots and to show both outcomes in the first five
(animated) shots. If you change a demo's state or shot count, find a new seed
that passes `MeasurementSeeds_LandOnTheTrueProbability`.
