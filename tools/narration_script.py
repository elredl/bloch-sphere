#!/usr/bin/env python3
"""The narration script, and its check against the lesson timeline.

The script text lives here (LINES), so every edit is re-checked: run from the project root
    python3 tools/narration_script.py
It also writes docs/narration_prompter.html, a page that keeps time while you record.
It fails if a line runs into a pause (the pauses must be silent in the recording), overlaps
another line, or needs more than MAX_RATE words per second, and writes docs/NARRATION_SCRIPT.md.

Rules for the text (spec, experimental invariant): one narration for all three conditions.
It never mentions predicting, grabbing, buttons or feedback (Condition 3 only), never poses
the pause questions, and never gives a pause's answer before that pause.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
TIMELINE = json.loads((ROOT / "Assets/Study/Data/lesson_timeline.json").read_text(encoding="utf-8"))
OUT = ROOT / "docs/NARRATION_SCRIPT.md"
PROMPTER = ROOT / "docs/narration_prompter.html"
MAX_RATE = 2.3   # words per second (about 140 a minute); the draft stays under 2.0

# (start s, end s, what is on screen, text). Times are lesson time in the recording.
LINES = [
# Segment 1
(0,14,"|0⟩ (from 1 s)","Welcome. In the next fifteen minutes you will learn how a quantum bit, called a qubit, works. You do not need any background in physics or math."),
(14,40,"","An ordinary bit in a computer is always either zero or one. A qubit is different. Until it is measured, it can be in a combination of zero and one. This combination is called a superposition."),
(40,70,"","In front of you is a picture of one qubit. The glowing arrow shows its state. When the arrow points straight up, the qubit is in the state called ket zero, written with a bar and an angle bracket around the zero. When it points straight down, it is in the state ket one."),
(70,100,"","To find out what a qubit holds, we measure it. A measurement always gives one of just two answers: zero or one. If the arrow points straight up, a measurement gives zero every time. If it points straight down, it gives one every time."),
(100,132,"","When the arrow points somewhere in between, the qubit is in a superposition. Then a measurement can give either answer, and which one you get is a matter of chance. The closer the arrow is to the top, the more often you get zero. The closer it is to the bottom, the more often you get one."),
(132,150,"","After each part of this lesson, the narration stops for one minute. Right now the arrow points straight up, at ket zero. Next, it will get a quarter turn toward ket one."),
# Segment 2
(210,243,"|+⟩; θ/φ arcs from 215","This picture is called the Bloch sphere. Every possible state of one qubit is a point on its surface, and the arrow always reaches from the center out to that point. The sphere has three axes. The z axis runs up and down, from ket zero at the top to ket one at the bottom."),
(243,270,"","The x and y axes lie flat, across the middle of the sphere, which we call the equator. Where they meet the surface are four more named states. Along x are ket plus and ket minus. Along y are ket plus i and ket minus i."),
(270,298,"","Two angles describe where the arrow points. The angle theta tells how far the arrow has tipped down from the top. The angle phi tells how far it has swung around the equator, starting from the x axis. Right now the arrow points along x, at ket plus."),
(298,302,"","Watch it turn."),
(302,336,"Rz(180) arc 300-303: |+⟩ → |−⟩","Every change to a qubit is a rotation of the arrow about an axis. That was a half turn about the z axis. The arrow swung around the equator, from ket plus to ket minus. Its height did not change, so theta stayed the same. Only phi changed."),
(336,340,"","Here is the same half turn again."),
(343,358,"Rz(180) arc 340-343: |−⟩ → |+⟩","The arrow is back at ket plus. Two half turns about the same axis make one full turn."),
(358,390,"θ/φ arcs hidden at 380","Every turn has a direction. Point the thumb of your right hand along the axis. Your fingers then curl the way the arrow turns. For the z axis, with your thumb pointing up, that is counterclockwise when seen from above. Next, the arrow at ket plus will get a quarter turn about the z axis."),
# Segment 3
(450,478,"re-prepared to |0⟩ at 452","In quantum computing, the basic operations on a qubit are called gates. Each gate is a fixed rotation of the arrow. Here are three of them. The arrow has been set back to ket zero, at the top."),
(478,481,"","The X gate. Watch."),
(483,508,"X arc 480-483: |0⟩ → |1⟩","The X gate is a half turn about the x axis. It takes ket zero at the top to ket one at the bottom. It flips the qubit, like switching an ordinary bit between zero and one."),
(519,529,"re-prepared to |+⟩ at 520","Now the arrow is set to ket plus, on the equator. The second gate is the Z gate. Watch."),
(533,556,"Z arc 530-533: |+⟩ → |−⟩","The Z gate is a half turn about the z axis. It swings ket plus around the equator to ket minus. Like every turn about z, it changes phi and leaves the height alone."),
(556,576,"","The third gate is called H, after the mathematician Hadamard. H is a half turn about a tilted axis, halfway between the x axis and the z axis."),
(579,589,"re-prepared to |0⟩ at 580","The arrow is set back to ket zero. Now watch H."),
(593,630,"H arc 590-593: |0⟩ → |+⟩","H took ket zero at the top to ket plus on the equator. It turned a definite state into an equal superposition. Gates can be used one after another, and each one is just another turn of the same arrow. The arrow is now at ket plus. Next, H will be applied again."),
# Segment 4
(690,718,"|0⟩","Now let us look more closely at measurement. A measurement gives zero or one, and it also changes the qubit. Right after the measurement, the arrow points straight at the answer you got: up for zero, down for one."),
(718,722,"","First, an H gate."),
(723,744,"H arc 720-723: |0⟩ → |+⟩","The arrow is now at ket plus, on the equator. Measuring it once tells us very little: we get a single zero or a single one."),
(744,760,"","So the same state is prepared again and again, and measured each time. The panel counts the results. Watch."),
(770,800,"measurement demo 760-769.5 (100 shots of |+⟩); panel until 820","Out of one hundred measurements, about half gave zero and half gave one. Each single result is chance, but over many measurements the counts follow the state."),
(800,826,"panel hidden at 820","The chance of zero depends on how high the tip of the arrow sits. At the top, every measurement gives zero. On the equator, it is half and half. In between, it follows the height of the tip, not the angle the arrow has turned."),
(826,840,"re-prepared to |0⟩ at 830","Next, the arrow will be set back to ket zero, get a sixty degree turn toward ket one, and then be measured one hundred times."),
]

# Decisions in the draft that need a yes or no before recording.
REVIEW = [
    "No pause is recapped afterwards: the narration never says what a pause's operation did, so "
    "Conditions 1 and 2 get no correct answer that Condition 3 doesn't also see. The next segment "
    "just carries on from the visible state.",
    "Pause 2: segment 2 teaches the turn direction (right-hand rule; about z, counterclockwise seen "
    "from above). Without it the answer is a guess; with it the pause tests applying the rule.",
    "Pause 3: segment 3 shows H taking ket zero to ket plus and says it makes a superposition, but "
    "never says H undoes itself. That is what pause 3 tests.",
    "Pause 4: segment 4 says the chance follows the height of the tip, not the angle. That steers "
    "away from the 67 % answer without giving 75 %. Remove the last clause if that is too much help.",
    "Each pre-pause line names the coming operation (\"Next, it will get a quarter turn toward ket "
    "one\") but asks no question, so no condition gets a prediction prompt from the narration.",
    "Before pause 1 the narration says it stops for a minute after each part. This is the only "
    "description of the pauses, and it is true in every condition.",
]

SEGMENT_TITLES = {"superposition": "Superposition", "bloch_sphere": "The Bloch sphere",
                  "gates": "Single-qubit gates", "measurement": "Measurement"}


def words(text):
    return len(re.findall(r"[A-Za-z']+", text))


def check():
    errors = []
    pauses = [(s["pauseStart"], s["pauseEnd"]) for s in TIMELINE["segments"]]
    previous_end = 0
    for start, end, _, text in LINES:
        if end <= start:
            errors.append(f"{start}-{end}: ends before it starts")
        if start < previous_end:
            errors.append(f"{start}-{end}: overlaps the line before (ends {previous_end})")
        for p0, p1 in pauses:
            if start < p1 and end > p0:
                errors.append(f"{start}-{end}: runs into the pause {p0}-{p1}, which must be silent")
        if end > TIMELINE["lessonDuration"]:
            errors.append(f"{start}-{end}: past the end of the lesson")
        rate = words(text) / (end - start)
        if rate > MAX_RATE:
            errors.append(f"{start}-{end}: {rate:.2f} words/s, more than {MAX_RATE}")
        previous_end = end
    return errors


def pace():
    return sum(words(t) for _, _, _, t in LINES) / sum(e - s for s, e, _, _ in LINES) * 60


def max_rate():
    return max(words(t) / (e - s) for s, e, _, t in LINES) * 60


def clock(seconds):
    return f"{int(seconds // 60)}:{int(seconds % 60):02d}"


def render():
    out = [
        "# Narration script",
        "",
        "Generated by `tools/narration_script.py` (edit the text there, then run it: it checks the",
        "timing against `Assets/Study/Data/lesson_timeline.json` and rewrites this file).",
        "Draft for review. One recording for all three conditions.",
        "",
        "## Recording",
        "",
        f"- One file, exactly {TIMELINE['lessonDuration']:.1f} s (15:00.0), mono WAV, 48 kHz.",
        "- Each line starts at its time and must be finished by its end time. Lines with an",
        "  on-screen cue (an arc, a re-preparation, the measurement demo) should land on it;",
        "  \"Watch\" lines come just before the arc starts.",
        "- The four pauses are digital silence, exactly as listed. No music or room tone in them.",
        f"- Calm, even pace: {pace():.0f} words a minute while speaking (no line above {max_rate():.0f}).",
        "- In the app: the clip goes in the Narration field of `LessonController` (Lesson scene);",
        "  set it in `StyleLabSetup.BuildLessonScene` too, or the next scene rebuild clears it.",
        "  The app refuses to start if the clip length differs from the timeline by more than 0.5 s.",
        "  If a cue has to move, edit only the event times in `lesson_timeline.json`; segment and",
        "  pause times are fixed (spec).",
        "",
        "Say it as written: |0⟩ is \"ket zero\", |1⟩ \"ket one\", |+⟩ \"ket plus\", |−⟩ \"ket minus\",",
        "|+i⟩ \"ket plus i\", |−i⟩ \"ket minus i\", θ \"theta\" (THAY-ta), φ \"phi\" (FYE), Hadamard (HA-da-mar).",
        "",
    ]
    segments = TIMELINE["segments"]
    for i, seg in enumerate(segments):
        out += [f"## {i + 1}. {SEGMENT_TITLES.get(seg['id'], seg['id'])} ({clock(seg['start'])} to {clock(seg['pauseStart'])})", "",
                "| Lesson time | On screen (lesson s) | Narration | Words |", "|---|---|---|---|"]
        for start, end, screen, text in LINES:
            if seg["start"] <= start < seg["pauseStart"]:
                out.append(f"| {start}-{end} s<br>{clock(start)}-{clock(end)} | {screen.replace('|', chr(92) + '|')} | {text} | {words(text)} |")
        out += ["", f"**Pause {i + 1}: {clock(seg['pauseStart'])} to {clock(seg['pauseEnd'])}, silence (60 s).**", ""]
    total = sum(words(t) for _, _, _, t in LINES)
    out += [f"Total: {total} words.", "", "## Points to review", ""] + [f"- {n}" for n in REVIEW] + [""]
    OUT.write_text("\n".join(out), encoding="utf-8")


def render_prompter():
    """A self-contained page that keeps time while you record: open it in a browser."""
    data = {
        "duration": TIMELINE["lessonDuration"],
        "lines": [{"start": a, "end": b, "screen": c, "text": t} for a, b, c, t in LINES],
        "pauses": [{"start": s["pauseStart"], "end": s["pauseEnd"], "n": i + 1} for i, s in enumerate(TIMELINE["segments"])],
        "segments": [{"start": s["start"], "title": SEGMENT_TITLES.get(s["id"], s["id"])} for s in TIMELINE["segments"]],
    }
    html = PROMPTER_TEMPLATE.replace("__DATA__", json.dumps(data, ensure_ascii=False))
    PROMPTER.write_text(html, encoding="utf-8")


PROMPTER_TEMPLATE = r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Narration prompter</title>
<style>
  body { margin: 0; background: #05060a; color: #e8ecf2; font: 18px/1.4 system-ui, sans-serif; }
  #top { display: flex; gap: 24px; align-items: baseline; padding: 16px 28px; border-bottom: 1px solid #1c2230; }
  #clock { font: 600 44px ui-monospace, monospace; color: #00f0ff; min-width: 6.5ch; }
  #segment { color: #8a93a6; flex: 1; }
  #state { font-weight: 600; letter-spacing: .08em; }
  .speak { color: #7dff9a; } .wait { color: #ffe14d; } .silent { color: #ff6b6b; }
  main { padding: 28px; max-width: 1100px; }
  #now { font-size: 38px; line-height: 1.35; min-height: 5.4em; }
  #now.waiting { color: #6b7385; }
  #bar { height: 10px; background: #1c2230; border-radius: 5px; overflow: hidden; margin: 18px 0 6px; }
  #fill { height: 100%; width: 0; background: #00f0ff; }
  #barText { color: #8a93a6; font-size: 15px; }
  #screen { color: #ff9e1b; margin-top: 18px; min-height: 1.4em; }
  #next { color: #6b7385; font-size: 22px; margin-top: 26px; border-top: 1px solid #1c2230; padding-top: 14px; }
  #controls { padding: 0 28px 28px; color: #8a93a6; font-size: 15px; }
  button { background: #0d1220; color: #e8ecf2; border: 1px solid #00f0ff66; border-radius: 4px; padding: 6px 12px; margin: 0 6px 6px 0; font-size: 15px; cursor: pointer; }
  #big { font: 600 120px ui-monospace, monospace; color: #ffe14d; text-align: center; padding-top: 30px; }
</style></head><body>
<div id="top"><div id="clock">0:00.0</div><div id="segment"></div><div id="state"></div></div>
<main>
  <div id="big"></div>
  <div id="now" class="waiting">Start your recording, then press Space. A 3-second count-in comes first; the lesson clock starts at 0:00.0 after it.</div>
  <div id="bar"><div id="fill"></div></div><div id="barText"></div>
  <div id="screen"></div>
  <div id="next"></div>
</main>
<div id="controls">
  <div>Space: start / pause &nbsp; R: back to the start &nbsp; Starting at a segment: the clock shows lesson time, so you can line the take up later. Any time: add #t=seconds to the address, e.g. #t=480.</div>
  <div id="jumps"></div>
</div>
<script>
const D = __DATA__;
const $ = id => document.getElementById(id);
let running = false, countIn = 0, offset = 0, startedAt = 0, from = 0;
const clock = s => { const m = Math.floor(s / 60), r = s - m * 60; return m + ":" + (r < 10 ? "0" : "") + r.toFixed(1); };
function now() { return running ? from + (performance.now() - startedAt) / 1000 : from; }
function start() {
  if (running || countIn) return;
  countIn = 3; $("big").textContent = countIn; $("now").textContent = ""; $("next").textContent = "";
  const tick = setInterval(() => {
    countIn--;
    if (countIn > 0) { $("big").textContent = countIn; return; }
    clearInterval(tick); $("big").textContent = ""; running = true; startedAt = performance.now();
  }, 1000);
}
function pause() { if (!running) return; from = now(); running = false; }
function jump(t) { running = false; countIn = 0; from = t; $("big").textContent = ""; draw(); }
document.addEventListener("keydown", e => {
  if (e.code === "Space") { e.preventDefault(); running ? pause() : start(); }
  if (e.key === "r" || e.key === "R") jump(0);
});
D.segments.forEach((s, i) => {
  const b = document.createElement("button"); b.textContent = (i + 1) + ". " + s.title + " (" + clock(s.start) + ")";
  b.onclick = () => jump(s.start); $("jumps").appendChild(b);
});
function draw() {
  const t = Math.min(now(), D.duration);
  $("clock").textContent = clock(t);
  const seg = [...D.segments].reverse().find(s => t >= s.start);
  $("segment").textContent = seg ? seg.title : "";
  const pause = D.pauses.find(p => t >= p.start && t < p.end);
  const line = D.lines.find(l => t >= l.start && t < l.end);
  const upcoming = D.lines.find(l => l.start > t);
  let state = "", cls = "", text = "", frac = 0, barText = "", screen = "";
  if (t >= D.duration) { state = "END"; cls = "silent"; text = "End of the lesson (15:00.0). Stop the recording."; }
  else if (pause) {
    state = "SILENCE"; cls = "silent"; frac = (t - pause.start) / (pause.end - pause.start);
    text = "Pause " + pause.n + ": silence."; barText = (pause.end - t).toFixed(0) + " s of silence left";
  } else if (line) {
    state = "SPEAK"; cls = "speak"; text = line.text; screen = line.screen;
    frac = (t - line.start) / (line.end - line.start); barText = (line.end - t).toFixed(1) + " s left for this line";
  } else if (upcoming) {
    state = "WAIT"; cls = "wait"; text = upcoming.text; screen = upcoming.screen;
    barText = "starts in " + (upcoming.start - t).toFixed(1) + " s";
  }
  $("state").textContent = state; $("state").className = cls;
  $("now").textContent = countIn ? "" : text; $("now").className = state === "WAIT" ? "waiting" : "";
  $("fill").style.width = (frac * 100).toFixed(1) + "%";
  $("fill").style.background = state === "SILENCE" ? "#ff6b6b" : "#00f0ff";
  $("barText").textContent = countIn ? "" : barText;
  $("screen").textContent = screen ? "On screen: " + screen : "";
  const after = line ? D.lines.find(l => l.start >= line.end) : (state === "WAIT" ? D.lines.find(l => l.start > upcoming.start) : upcoming);
  const nextPause = D.pauses.find(p => p.start >= t);
  let next = after ? "Next (" + clock(after.start) + "): " + after.text : "";
  if (nextPause && after && nextPause.start < after.start) next = "Next: pause " + nextPause.n + " at " + clock(nextPause.start) + " (silence)";
  $("next").textContent = countIn ? "" : next;
}
const hashT = /t=([0-9.]+)/.exec(location.hash); if (hashT) jump(parseFloat(hashT[1]));
(function loop() { draw(); requestAnimationFrame(loop); })();
</script></body></html>
"""


if __name__ == "__main__":
    problems = check()
    for p in problems:
        print("ERROR", p)
    render()
    render_prompter()
    print(f"wrote {OUT.relative_to(ROOT)} and {PROMPTER.relative_to(ROOT)}; {len(problems)} timing errors")
    sys.exit(1 if problems else 0)
