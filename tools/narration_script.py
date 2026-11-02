#!/usr/bin/env python3
"""The lesson text, the scripted events between its boxes, and their check.

The lesson is self-paced: a text box shows with Next; after Next the box goes and the
animations that follow play (Condition 3 makes each turn itself), each followed by Next;
then the next box. SCRIPT below is the one place that order lives. Run from the project root
    python3 tools/narration_script.py
It lays the lesson out on lesson time (a box gets time to be read aloud at LAYOUT_RATE,
each event its own length; each segment as long as its content, each pause pauseLength),
and writes
    Assets/Study/Data/narration_captions.json   the boxes (shown in every condition)
    Assets/Study/Data/lesson_timeline.json      its events, segment times and lessonDuration
                                                (pause tasks untouched)
    docs/NARRATION_SCRIPT.md                    the script for review and recording
Lesson time still matters: it orders everything, and a recorded narration would be cut to
the box times. Next jumps over any time left, so a learner never waits on it.

With --demo it does the same for the in-person demo app (DEMO_SCRIPT: one segment whose
pause is the sandbox) into demo_captions.json, demo_timeline.json and docs/DEMO_SCRIPT.md.
The demo is Condition 3 only and never part of the study, so its text may say "you".

Rules for the study text (spec, experimental invariant): one text for all three conditions.
It never mentions predicting, grabbing, buttons or feedback, never says who makes a turn
("next, X is applied", not "watch" or "turn it"), never poses the pause questions, and
never shows a pause's own operation before that pause. It does teach what each pause
needs (the equator is fifty-fifty, a turn about z keeps the height), so every pause can be
answered from the lesson.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEMO = "--demo" in sys.argv
DATA = ROOT / "Assets/Study/Data"
TIMELINE_PATH = DATA / ("demo_timeline.json" if DEMO else "lesson_timeline.json")
TIMELINE = json.loads(TIMELINE_PATH.read_text(encoding="utf-8"))
OUT = ROOT / ("docs/DEMO_SCRIPT.md" if DEMO else "docs/NARRATION_SCRIPT.md")
CAPTIONS = DATA / ("demo_captions.json" if DEMO else "narration_captions.json")
LAYOUT_RATE = 2.3     # words per second a box is laid out at (about 140 a minute)
MIN_BOX = 3.0         # seconds, the shortest box
GAP = 0.5             # seconds between items
ARC = TIMELINE["pauseSchedule"]["arcDuration"]
REPREPARE = TIMELINE["reprepareDuration"]
DEMO_SECONDS = 9.5    # MeasurementDemo.Duration

# One list per segment, in order. Items:
#   (title, text)          a box (title in capitals, like the intro's boxes)
#   (title, text, shown)   a box with a line shown under the text (a formula), not read aloud
#   ("gate", name)         a turn (Gate.FromName), its own step with Next
#   ("set", state)         re-preparation (fade), no Next of its own
#   ("demo", state, shots, seed)   the repeated-measurement panel, its own step with Next
#   ("hide", target)
SCRIPT = [
# Segment 1: one qubit on the Bloch sphere (ends at |0>, pause 1: quarter turn toward |1>)
[
    ("set", "0"),
    ("BITS AND QUBITS", "An ordinary bit is always either zero or one. A qubit can be in a combination of both until it is measured. This combination is called a superposition. The glowing arrow shows the state of one qubit."),
    ("KET ZERO", "Right now the arrow points straight up, at ket zero. To find out what a qubit holds, we measure it. A measurement gives one of just two answers: zero or one. At ket zero, it gives zero every time. Next, the arrow is turned all the way down."),
    ("gate", "Ry(180)"),
    ("KET ONE", "Straight down is ket one. Here a measurement gives one every time. The very top and the very bottom are the only places where the answer is certain."),
    ("IN BETWEEN", "Anywhere between the top and the bottom, the qubit is in a superposition. A measurement can then give either answer, and which one you get is a matter of chance. The higher the tip, the more often you get zero. Exactly halfway down, zero and one are equally likely."),
    ("THE THREE AXES", "The sphere has three axes. The z axis runs up and down, from ket zero to ket one. The x and y axes lie flat across the middle. The circle around the middle of the sphere is called the equator."),
    ("ON THE EQUATOR", "Four points on the equator have names. Along x are ket plus and ket minus. Along y are ket plus i and ket minus i."),
    ("set", "+"),
    ("AROUND THE EQUATOR", "The arrow is now at ket plus. Next, it moves around the equator a quarter turn at a time, through the other three named states. Watch its height as it goes."),
    ("gate", "Rz(90)"),
    ("gate", "Rz(90)"),
    ("gate", "Rz(90)"),
    ("SAME HEIGHT, SAME ODDS", "That was ket plus i, ket minus, and ket minus i. The height never changed, so every one of these states gives zero half the time. A measurement cannot tell them apart. So what makes them different states? Keep that question in mind."),
    ("set", "0"),
    ("YOUR TURN TO PREDICT", "After each part of this lesson there is a short exercise. The arrow is back at ket zero. Next, it gets a quarter turn toward ket one."),
],
# Segment 2: gates are turns (starts at |+>; ends at |+i>, pause 2: S)
[
    ("GATES", "Every change to a qubit, short of measuring it, is a turn of the arrow about an axis. A fixed turn that quantum computers use again and again is called a gate. The arrow is at ket plus, where the exercise left it."),
    ("THE Z GATE", "The Z gate is a half turn about the z axis. Next, Z is applied to ket plus."),
    ("gate", "Z"),
    ("WHAT Z DID", "Z swung the arrow halfway around the equator, to ket minus. Its height did not change, so the odds did not change either: ket minus still gives zero half the time. Z changed something real that a measurement cannot see. This direction around the equator is the phase."),
    ("set", "+"),
    ("TURN DIRECTION", "Every turn has a direction. Point the thumb of your right hand along the axis. Your fingers curl the way the arrow turns. For the z axis, with your thumb pointing up, that is counterclockwise when seen from above."),
    ("THE S GATE", "The S gate is a quarter turn about the z axis. Next, S is applied to ket plus. Use your right hand: where will it land?"),
    ("gate", "S"),
    ("WHAT S DID", "S took ket plus a quarter turn counterclockwise, to ket plus i. Like Z, it changed only the phase. The height, and so the odds, stayed the same."),
    ("set", "0"),
    ("THE X GATE", "Not every gate turns about z. The X gate is a half turn about the x axis. The arrow is at ket zero. Next, X is applied."),
    ("gate", "X"),
    ("WHAT X DID", "X took ket zero at the top to ket one at the bottom. This time the height changed, so the odds changed too: from always zero to always one. X flips a qubit, just like flipping an ordinary bit."),
    ("THE Y GATE", "The Y gate is a half turn about the y axis. It also flips the arrow, but about a different axis. Next, Y is applied to ket one."),
    ("gate", "Y"),
    ("THE H GATE", "The arrow is back at ket zero. The H gate is named after the mathematician Hadamard. It is a half turn about a tilted axis, halfway between the x axis and the z axis. Next, H is applied to ket zero."),
    ("gate", "H"),
    ("WHAT H DID", "H took ket zero from the top down to ket plus on the equator: from a certain answer to an equal superposition. Z and S only move the arrow around the equator. H can move it between top and equator. Next, S is applied."),
    ("gate", "S"),
    ("WHAT COMES NEXT", "S took ket plus to ket plus i. For the exercise, the arrow will get one more S."),
],
# Segment 3: undoing and combining gates (starts at |->; ends at |->, pause 3: H)
[
    ("set", "0"),
    ("UNDOING GATES", "Every gate is a turn, so every gate can be undone by turning back. The arrow is at ket zero. Next, it gets two X gates in a row."),
    ("gate", "X"),
    ("gate", "X"),
    ("X UNDOES X", "Two half turns about the same axis make one full turn, so the second X undid the first."),
    ("set", "+"),
    ("FOUR S GATES", "The arrow is at ket plus. Next, it gets four S gates in a row."),
    ("gate", "S"),
    ("gate", "S"),
    ("gate", "S"),
    ("gate", "S"),
    ("FOUR QUARTER TURNS", "Four quarter turns make a full turn, so the arrow is back at ket plus. Along the way, two S gates did the same job as one Z: a half turn about z."),
    ("set", "0"),
    ("H TWICE", "The arrow is at ket zero. Next, it gets H twice."),
    ("gate", "H"),
    ("gate", "H"),
    ("H UNDOES ITSELF", "H, then H again, brings ket zero back to ket zero. Now back to the question from Part 1. Next, ket zero gets an H again, but this time a Z follows."),
    ("gate", "H"),
    ("gate", "Z"),
    ("WHAT COMES NEXT", "The arrow is at ket minus. As far as a measurement can tell, nothing changed: ket plus and ket minus both give zero half the time. Next, H is applied again, just as before."),
],
# Segment 4: interference and measurement (starts at |1>; ends at |0>, pause 4: 60 deg turn, 100 shots)
[
    ("WHAT JUST HAPPENED", "H then H brought ket zero back to ket zero. H, Z, H landed on ket one, the opposite answer. The only difference was a Z, which on its own changed nothing a measurement could see. H turned that hidden phase into height. This is called interference, and quantum algorithms are built on it. It is also why a qubit needs complex numbers: they carry the phase."),
    ("MEASUREMENT", "Now a closer look at measurement. A measurement gives zero or one, and it also changes the qubit. Right after, the arrow points straight at the answer you got: up for zero, down for one."),
    ("set", "0"),
    ("SETTING UP", "The arrow is at ket zero. Next, it gets an H."),
    ("gate", "H"),
    ("ONE RESULT IS NOT ENOUGH", "The arrow is at ket plus, on the equator. Measuring it once gives a single zero or a single one, which tells us very little. So the same state is prepared again and again, and measured each time. The panel counts the results."),
    ("demo", "+", 100, 27),
    ("ONE HUNDRED RESULTS", "About half gave zero and half gave one. Each single result is chance, but over many measurements the counts follow the state."),
    ("hide", "measurement_panel"),
    ("THE HEIGHT RULE", "Read the height of the tip on a scale from minus one at the bottom to plus one at the top. The chance of zero is one plus the height, divided by two. At the top, that is one hundred percent. On the equator, fifty. At the bottom, zero.",
     "P(0) = (1 + height) / 2"),
    ("HEIGHT, NOT ANGLE", "The chance follows the height of the tip, not how far the arrow has turned. Near the top, a turn barely changes the height. Near the equator, the same turn changes it a lot. This chance is the size of alpha squared from the start of the lesson."),
    ("MEASURING AGAIN", "Measuring again right away gives the same answer every time, because the arrow already points at it. And unlike a gate, a measurement cannot be undone by turning back."),
    ("WHAT A MEASUREMENT SEES", "A measurement only reads height. So it always tells apart opposite points at the top and bottom, like ket zero and ket one. Ket plus and ket minus are opposite too, but at the same height. That is why, in Part 3, an H was needed to tell them apart."),
    ("set", "0"),
    ("WHAT COMES NEXT", "The arrow is back at ket zero. Next, it gets a sixty degree turn toward ket one, and is then measured one hundred times."),
],
]

# The in-person demo (about a minute of reading, then the sandbox). Ends at ket minus.
DEMO_SCRIPT = [
[
    ("set", "0"),
    ("THE BLOCH SPHERE", "This sphere shows the state of one qubit, the quantum version of a bit. The glowing arrow is its state. Straight up is ket zero, straight down is ket one."),
    ("A GATE IS A TURN", "Every change to a qubit is a turn of the arrow about an axis. The X gate is a half turn about the x axis. Next, you make it yourself: X takes ket zero to ket one."),
    ("gate", "X"),
    ("SUPERPOSITION", "X flipped the qubit, like an ordinary bit. The H gate is a half turn about a tilted axis, between x and z. Next, H takes the arrow from ket one down onto the equator."),
    ("gate", "H"),
    ("MEASUREMENT", "On the equator the qubit is in a superposition: a measurement gives zero or one by chance. Next, this state is prepared and measured one hundred times."),
    ("demo", "-", 100, 27),
    ("YOUR TURN", "About half zeros and half ones. Now explore: apply gates, grab the arrow to turn it anywhere, and measure. Press Done when you are finished."),
    ("hide", "measurement_panel"),
],
]
if DEMO:
    SCRIPT = DEMO_SCRIPT

# Decisions in the draft that need a yes or no.
REVIEW = [
    "Revised script (Nov 2, 2026): one storyline. Height sets the odds; the direction around the "
    "equator (phase) is hidden until a gate like H turns it into height. Part 1 raises the question, "
    "Part 2 names phase, Part 3 shows interference, Part 4 makes the height rule quantitative.",
    "Boxes never say who makes a turn (\"Next, X is applied\"), so the text is the same where the "
    "learner makes the turn (Condition 3) and where it plays by itself.",
    "Turns that only returned the arrow to a starting point are silent re-preparations now, not "
    "steps. Condition 3 makes 21 turns, down from 28: a smaller dose of the agency manipulation.",
    "Pause 1 is answerable from the text (\"exactly halfway down, zero and one are equally likely\"); "
    "pause 2 from \"like Z, it changed only the phase\"; pause 3 sets up H, Z, H without its result; "
    "pause 4's formula and \"height, not angle\" give the rule, not the 60 degree answer.",
    "The boxes are longer on average. Read them in the headset and cut any box that takes over "
    "about 20 seconds.",
    "The θ/φ angle arcs are cut (itinerary open decision 1).",
]

SEGMENT_TITLES = ["Demo"] if DEMO else ["One qubit on the Bloch sphere", "Gates are turns", "Undoing and combining gates", "Interference and measurement"]
if DEMO:
    REVIEW = ["The demo app only: Condition 3, never part of the study. Its pause is the sandbox (DemoSandbox)."]


def words(text):
    return len(re.findall(r"[A-Za-z']+", text))


KINDS = ("gate", "set", "demo", "hide")


def layout():
    """Lesson times for every item: (boxes, events, segments, errors). boxes: (segment, start, end, title, text)."""
    boxes, events, segments, errors = [], [], [], []
    if len(SCRIPT) != len(TIMELINE["segments"]):
        errors.append(f"SCRIPT has {len(SCRIPT)} segments, the timeline {len(TIMELINE['segments'])}")
    start = 0.0
    for index, (segment, items) in enumerate(zip(TIMELINE["segments"], SCRIPT)):
        t = start + 1.0
        for item in items:
            t = round(t, 1)
            kind = item[0]
            if kind not in KINDS:
                title, text = item[0], item[1]
                length = max(MIN_BOX, round(words(text) / LAYOUT_RATE, 1))
                boxes.append((index, t, round(t + length, 1), title, text, item[2] if len(item) > 2 else None))
                t += length + GAP
                continue
            if kind == "gate":
                events.append({"t": t, "type": "apply_gate", "gate": item[1], "duration": ARC})
                t += ARC + GAP
            elif kind == "set":
                events.append({"t": t, "type": "set_state", "state": item[1]})
                t += REPREPARE + GAP
            elif kind == "demo":
                events.append({"t": t, "type": "measure_demo", "state": item[1], "shots": item[2], "seed": item[3]})
                t += DEMO_SECONDS + GAP
            elif kind == "hide":
                events.append({"t": t, "type": "hide", "target": item[1]})
                t += GAP
            else:
                errors.append(f"segment {index + 1}: unknown item {item}")
        pause_start = float(round(t + 0.5))
        segments.append({**segment, "start": start, "pauseStart": pause_start,
                         "pauseEnd": pause_start + TIMELINE["pauseSchedule"]["pauseLength"]})
        start = segments[-1]["pauseEnd"]
    return boxes, events, segments, errors


# Spoken words back to the symbols on screen, longest first ("ket plus i" before "ket plus").
SYMBOLS = [("the size of alpha squared", "|α|²"), ("ket plus i", "|+i⟩"), ("ket minus i", "|−i⟩"), ("ket zero", "|0⟩"), ("ket one", "|1⟩"),
           ("ket plus", "|+⟩"), ("ket minus", "|−⟩"), ("theta", "θ"), ("phi", "φ")]


def caption(text):
    for spoken, symbol in SYMBOLS:
        text = re.sub(rf"\b{spoken}\b", symbol, text)
    return text


def write_captions(boxes):
    lines = [{"start": start, "end": end, "title": title, "text": caption(text) + (f"\n\n<align=center><size=130%>{shown}</size></align>" if shown else "")}
             for _, start, end, title, text, shown in boxes]
    body = ",\n".join("        " + json.dumps(line, ensure_ascii=False) for line in lines)
    CAPTIONS.write_text("{\n    \"lines\": [\n" + body + "\n    ]\n}\n", encoding="utf-8")


def replace_array(text, key, rows):
    start = text.index(f'"{key}": [')
    end = text.index("],", start) + 1
    body = ",\n".join("        " + json.dumps(r, ensure_ascii=False) for r in rows)
    return text[:start] + f'"{key}": [\n' + body + "\n    ]" + text[end:]


def write_timeline(events, segments):
    """Replaces the events, the segments and lessonDuration, keeping the file's layout elsewhere."""
    text = TIMELINE_PATH.read_text(encoding="utf-8")
    text = replace_array(text, "events", events)
    text = replace_array(text, "segments", segments)
    text = re.sub(r'"lessonDuration": [0-9.]+', f'"lessonDuration": {segments[-1]["pauseEnd"]:.1f}', text)
    TIMELINE_PATH.write_text(text, encoding="utf-8")


def clock(seconds):
    return f"{int(seconds // 60)}:{int(seconds % 60):02d}"


def describe(item):
    kind = item[0]
    if kind == "gate":
        return f"turn: {item[1]} (Next)"
    if kind == "set":
        return f"re-prepared to {caption('|' + item[1] + '⟩')}"
    if kind == "demo":
        return f"measurement demo: {item[2]} shots of |{item[1]}⟩ (Next)"
    return f"{kind} {item[1]}"


def render(boxes):
    out = [
        "# Demo script" if DEMO else "# Lesson script",
        "",
        "Generated by `tools/narration_script.py` (edit the text there, then run it: it lays the",
        "boxes and events out on lesson time, writes the captions and the timeline's events, and",
        "rewrites this file). Draft for review. One text for all three conditions.",
        "",
        "## How it plays",
        "",
        "- Self-paced. Each box shows with a Next button; after Next the box goes.",
        "- Each turn is its own step: Conditions 1 and 2 watch it play, Condition 3 makes it",
        "  (grab the tip, turn it along the guide). Next shows once the turn is done.",
        "- The measurement demo is a step too; re-preparations happen as the next box appears.",
        "- Lesson times below order everything; Next jumps over any time left, so nobody waits.",
        "",
        "If this is recorded, say it as written: |0⟩ is \"ket zero\", |1⟩ \"ket one\", |+⟩ \"ket plus\",",
        "|−⟩ \"ket minus\", |+i⟩ \"ket plus i\", |−i⟩ \"ket minus i\", Hadamard (HA-da-mar).",
        "",
    ]
    box_iter = iter(boxes)
    for i, items in enumerate(SCRIPT):
        out += [f"## {i + 1}. {SEGMENT_TITLES[i]}", "", "| Lesson time | Step | Words |", "|---|---|---|"]
        for item in items:
            if item[0] not in KINDS:
                _, start, end, title, text, shown = next(box_iter)
                extra = f"<br>*shown:* {shown}" if shown else ""
                out.append(f"| {start:.1f}-{end:.1f} s | **{title}**<br>{text}{extra} | {words(text)} |")
            else:
                out.append(f"|  | *{describe(item).replace('|', chr(92) + '|')}* |  |")
        out += ["", "**Sandbox** (until Done)." if DEMO else f"**Pause {i + 1}** (untimed, ends with Next).", ""]
    total = sum(words(b[4]) for b in boxes)
    out += [f"Total: {total} words in {len(boxes)} boxes.", "", "## Points to review", ""] + [f"- {n}" for n in REVIEW] + [""]
    OUT.write_text("\n".join(out), encoding="utf-8")


if __name__ == "__main__":
    boxes, events, segments, problems = layout()
    for p in problems:
        print("ERROR", p)
    if problems:
        sys.exit(1)
    render(boxes)
    write_captions(boxes)
    write_timeline(events, segments)
    print(f"wrote {OUT.relative_to(ROOT)}, {CAPTIONS.relative_to(ROOT)} and the events in "
          f"{TIMELINE_PATH.relative_to(ROOT)}: {len(boxes)} boxes, {len(events)} events")
