# Lesson itinerary: revamp draft

Status: **proposal, not approved.** Nothing in the app reads this file. If it is
approved, the narration script, timeline, pause tasks and feedback get rewritten
from it (see "If approved" at the end). Until then `NARRATION_SCRIPT.md` and
`lesson_timeline.json` are the lesson.

## Why

- The current lesson lines up with 8 of the 31 QISCIT items, several of them
  one-step facts a novice may already get right (ceiling risk on the posttest).
- Old segments 1 and 2 overlap (both introduce the arrow, |0⟩ and |1⟩).
- Merging them frees a segment for reversibility and interference: single-qubit,
  built only on what the lesson already shows, and tested by QISCIT items that
  never name it. This takes the matched items from 8 to 17, from easy to hard.
- Interference also targets the misconception the paper names first (a quantum
  computer "tries every input at once", Meyer et al.).

## What stays the same

- 15:00 total, four segments, four 60 s pauses, at the **same times** as now
  (segment and pause boundaries in `lesson_timeline.json` do not move).
- Same pause flow per condition (dim, predict, gate press, rotate, feedback in
  Condition 3; still sphere in Conditions 1 and 2).
- Same two prediction types the app already has: `position` (six named states)
  and `p0` (chance of measuring 0).
- Only gates the app already supports (`Gate.cs`: X, Y, Z, H, S, T, Rx/Ry/Rz).
- The narration rules in `NARRATION_SCRIPT.md` ("Points to review"): no pause is
  recapped, pre-pause lines name the coming operation but ask no question.
- Pauses 1 and 4 are unchanged.

## Skeleton

| Segment | Narration | Pause | Topic | Word budget |
|---|---|---|---|---|
| 1 | 0:00-2:30 | 2:30-3:30 | One qubit on the Bloch sphere (old 1 + 2 merged) | ~210 |
| 2 | 3:30-6:30 | 6:30-7:30 | Gates are turns | ~255 |
| 3 | 7:30-10:30 | 10:30-11:30 | **New:** undoing and combining gates | ~255 |
| 4 | 11:30-14:00 | 14:00-15:00 | Measurement (extended) | ~210 |

Word budgets use the current script's rate (939 words over 660 s of narration,
about 85 words per minute).

## Segment 1: One qubit on the Bloch sphere (0:00-2:30)

Teaches:
- An ordinary bit is 0 or 1; a qubit can be in a superposition until measured.
- The arrow: up is |0⟩, down is |1⟩; measuring gives only 0 or 1.
- Up always gives 0, down always gives 1; in between, the result is chance.
- Every point on the surface is a different state, so there are endlessly many
  superpositions.
- The three axes and the four equator states |+⟩, |−⟩, |+i⟩, |−i⟩.

On screen (approximate): |0⟩ at 1 s; |1⟩ when "down" is named; |+⟩ when the
equator is named, then a tour of |+i⟩, |−⟩, |−i⟩; back to |0⟩ before the pause
(pause 1 starts at |0⟩).

Implemented on this branch in `lesson_timeline.json` (events, pause 2 and 3
tasks) and `pause_feedback.json` (pause 2 and 3 feedback); the narration is
still the placeholder until the itinerary is approved.

Cut from the old lesson to fit: the θ/φ angle arcs (open decision 1).

**Pause 1 (unchanged):** start |0⟩, quarter turn toward |1⟩ (`Ry(90)`), predict
the chance of 0. Correct: 50 %.

## Segment 2: Gates are turns (3:30-6:30)

Teaches:
- Every change to a qubit is a turn of the arrow about an axis; a gate is a
  fixed turn.
- Turn direction by the right-hand rule (about z: counterclockwise from above).
- Z: half turn about z (|+⟩ → |−⟩). S: quarter turn about z (|+⟩ → |+i⟩).
  T: eighth turn about z, so two T make one S.
- X: half turn about x (|0⟩ → |1⟩, flips like a classical bit). Y: half turn
  about y (named only).
- H: half turn about the axis halfway between x and z (|0⟩ → |+⟩); it turns a
  definite state into an equal superposition.

On screen (approximate, 3 s arcs, none overlapping): |+⟩, Z arc; |+⟩, S arc;
\|0⟩, X arc; \|0⟩, H arc; re-prepared to \|+i⟩ before the pause.

Pre-pause line: "The arrow is at ket plus i. Next, it will get an S."

**Pause 2 (new):** start |+i⟩, gate S (`position`). Correct: **|−⟩**. The
segment shows S once (|+⟩ → |+i⟩); the pause asks to apply the same rule again.

| Answer | What it suggests |
|---|---|
| \|−⟩ | correct |
| \|+⟩ | turned the wrong way (right-hand rule reversed) |
| \|−i⟩ | treated S as a half turn |
| \|+i⟩ | thinks the gate changes nothing |
| \|0⟩ or \|1⟩ | turned about the wrong axis |

## Segment 3 (new): Undoing and combining gates (7:30-10:30)

Teaches:
- Two half turns about the same axis make a full turn: X undoes X.
- Four quarter turns make a full turn: S is undone by three more S. Two S make a
  half turn, the same as four T.
- Every gate is a turn, so every gate can be undone by turning back.
- H undoes itself: H then H returns |0⟩ to |0⟩. (The old lesson left this to be
  inferred in pause 3; here it is taught.)
- Gates in a row can cancel or not, depending on what sits between them. This
  is interference, introduced without the word or the math.

On screen (approximate): |0⟩, X arc, X arc (back to |0⟩); |+⟩, four S arcs
(once around); |0⟩, H arc, H arc (back to |0⟩); |0⟩, H arc, Z arc, ending at |−⟩
for the pause.

Pre-pause line: "The arrow is at ket minus. Next, H will be applied again."

**Pause 3 (new):** start |−⟩, gate H (`position`). Correct: **|1⟩**. The learner
has just seen H, H return to |0⟩; the Z in between changes the outcome.

| Answer | What it suggests |
|---|---|
| \|1⟩ | correct |
| \|0⟩ | "H always undoes H": thinks the Z in between does not matter |
| \|−⟩ | thinks the gate changes nothing |
| \|+⟩ | thinks H undoes the Z instead |
| \|+i⟩ or \|−i⟩ | turned about the wrong axis |

## Segment 4: Measurement (11:30-14:00)

As now (|0⟩, H arc, 100-shot demo of |+⟩, the height rule), plus:
- Measuring again right away gives the same answer every time.
- One result tells you something: getting 0 means the state was not |1⟩.
- Opposite points on the sphere are called orthogonal: measurement tells them
  apart every time.
- Unlike a gate, a measurement cannot be undone by any gate.

On screen: as now, plus a re-prepare to |0⟩ at the start (692 s): pause 3 now
ends at |1⟩, where the old one ended at |0⟩.

Budget risk: the current segment 4 already uses about 185 of its ~210 words.
The four additions need cuts elsewhere in the segment (open decision 2).

**Pause 4 (unchanged):** start |0⟩, 60° turn toward |1⟩ (`Ry(60)`), predict the
chance of 0 with a 100-shot demo. Correct: 75 % (67 % is the "follows the angle"
answer).

## QISCIT coverage (question numbers only, no answers)

| Segment | Items targeted |
|---|---|
| 1 | 20, 30 |
| 2 | 8, 15 |
| 3 | 5, 6, 10, 19, 31; stretch: 17, 24 |
| 4 | 9, 21, 22, 23, 26, 28 |

17 items in total (8 now). The other 14 need two qubits, entanglement,
teleportation or decoherence and stay out of scope. Items 17 and 24 are stretch
items: they go one step past what is taught (17 names Y as an option; 24 needs
a small rotation), so they give the posttest headroom at the top. The grouping
is a judgment call; have it checked independently before fixing the item list.

## Open decisions

1. Drop the θ/φ angle arcs (cut above to fit segment 1), or keep them and trim
   elsewhere? The paper motivates the sphere design with polar/azimuthal
   confusion (Hu et al.), and `pause_feedback.json` has a
   `polar_azimuthal_confusion` entry.
2. Segment 4 word budget: which of the four additions to keep if they don't fit.
3. Teach Y, or only name it? Naming it helps items 17 and 24 without a demo.
4. Whether the revamp changes the paper's four lesson objectives (superposition,
   Bloch sphere, gates, measurement) enough to need a rewrite of §3.2.

## If approved

- `docs/NARRATION_SCRIPT.md`: rewrite segments 1-3 and extend 4, timed in the
  same table format.
- `Assets/Study/Data/lesson_timeline.json`: new events for segments 1-3; new
  `pauseTasks` for pauses 2 and 3 (start state, gate, choices, correct answer).
  Segment and pause times stay as they are.
- `Assets/Study/Data/pause_feedback.json`: misconception feedback for the new
  wrong answers of pauses 2 and 3 (tables above).
- Run the Edit Mode tests (`Study.Tests.EditMode`); they check the timeline
  rules and the pause start states.
- Paper §3.2: the QISCIT item list, mapped to the new objectives, fixed before
  data collection.
- IRB: check whether the submitted protocol describes the lesson topics; if so,
  an amendment may be needed.
- All three conditions get the same new lesson; the desktop build too.
