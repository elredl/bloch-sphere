#!/usr/bin/env python3
"""The QISCIT pretest and posttest, taken on the laptop in a browser.

    python3 tools/quiz.py [--data study_data/quiz] [--port 8765]

Opens the quiz in the default browser (press F11 for full screen). The experimenter enters
the participant code and the phase (pre or post); the participant then reads the
instructions and the glossary and answers the 17 items (tools/quiz/qiscit.json) one at a
time, with the glossary a button away. Answers are never shown as right or wrong.

Every event is written at once to <data>/<code>_quiz-<phase>_<yyyyMMdd-HHmm>.csv in the
session file's columns (spec, Telemetry), flushed to disk row by row. The answer key stays
here: the page never receives it. condition and stratum are left blank; tools/merge_quiz.py
fills them in when it appends the quiz rows to the participant's session file (pull_data
runs it). A finished quiz cannot be taken again under the same code and phase; an
unfinished one (closed browser, crash) can be restarted into the same file.

Events (fields as key=value):
    quiz_start     phase items instrument
    quiz_restart   phase                       an unfinished quiz started again
    quiz_glossary  open=1|0 item               item blank before the first item
    quiz_item      item shown=1                each time an item comes on screen
    quiz_choice    item choice                 every click on an option
    quiz_answer    item choice correct seconds  on leaving the item (Next or Back)
    quiz_end       phase score core stretch seconds answered
    quiz_abandoned phase seconds               another quiz started, or the program stopped
"""
import argparse
import csv
import datetime
import glob
import json
import os
import pathlib
import re
import threading
import time
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = pathlib.Path(__file__).resolve().parent.parent
HERE = pathlib.Path(__file__).resolve().parent / "quiz"
HEADER = ["utc", "session_s", "lesson_s", "participant", "condition", "stratum", "app_version", "event", "fields"]
APP_VERSION = "quiz-1.0"
CODE = re.compile(r"^P\d{2}$")

ITEMS = json.loads((HERE / "qiscit.json").read_text(encoding="utf-8"))
KEY = {item["number"]: item["answer"] for item in ITEMS["items"]}
STRETCH = {item["number"] for item in ITEMS["items"] if item["stretch"]}
PUBLIC = {**ITEMS, "items": [{k: v for k, v in item.items() if k != "answer"} for item in ITEMS["items"]]}


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


def quiz_files(data, code, phase):
    """Quiz files for this code and phase, here and anywhere under study_data (merged ones too)."""
    found = set(glob.glob(str(data / f"{code}_quiz-{phase}_*.csv")))
    found |= set(glob.glob(str(ROOT / "study_data" / "**" / f"{code}_quiz-{phase}_*.csv"), recursive=True))
    return sorted(found)


def finished(path):
    with open(path, encoding="utf-8") as f:
        return any(",quiz_end," in line for line in f)


class Quiz:
    """The one quiz running at a time."""

    def __init__(self, path, code, phase, restart):
        self.path, self.code, self.phase = path, code, phase
        self.started = time.monotonic()
        self.answers = {}
        new = not os.path.exists(path)
        self.file = open(path, "a", encoding="utf-8", newline="")
        self.writer = csv.writer(self.file, lineterminator="\n")
        if new:
            self.writer.writerow(HEADER)
        self.log("quiz_restart" if restart else "quiz_start",
                 f"phase={phase} items={len(KEY)} instrument=QISCIT-v1.0-17")

    def log(self, event, fields):
        self.writer.writerow([utc_now(), "", "", self.code, "", "", APP_VERSION, event, fields])
        self.file.flush()
        os.fsync(self.file.fileno())

    def seconds(self):
        return time.monotonic() - self.started

    def end(self):
        core = sum(1 for n, c in self.answers.items() if c == KEY[n] and n not in STRETCH)
        stretch = sum(1 for n, c in self.answers.items() if c == KEY[n] and n in STRETCH)
        self.log("quiz_end", f"phase={self.phase} score={core + stretch}/{len(KEY)} core={core}/{len(KEY) - len(STRETCH)} "
                             f"stretch={stretch}/{len(STRETCH)} seconds={self.seconds():.1f} answered={len(self.answers)}")
        self.file.close()


class Handler(BaseHTTPRequestHandler):
    data = None
    quiz = None
    lock = threading.Lock()

    def log_message(self, *args):
        pass

    def reply(self, status, body, kind="application/json"):
        raw = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", kind + ("; charset=utf-8" if not kind.startswith("image") else ""))
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self):
        if self.path in ("/", "/index.html"):
            self.reply(200, (HERE / "index.html").read_bytes(), "text/html")
        elif self.path == "/items":
            self.reply(200, PUBLIC)
        else:
            self.reply(404, {"error": "not found"})

    def do_POST(self):
        try:
            body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
        except ValueError:
            return self.reply(400, {"error": "bad request"})
        with Handler.lock:
            try:
                self.reply(200, self.handle_post(self.path, body))
            except QuizError as e:
                self.reply(409, {"error": str(e)})

    def handle_post(self, path, body):
        cls = Handler
        if path == "/check":
            code, phase = body.get("code", "").strip().upper(), body.get("phase", "")
            if not CODE.match(code):
                raise QuizError("Codes are P plus two digits, e.g. P07.")
            if phase not in ("pre", "post"):
                raise QuizError("Pick pre or post.")
            done = [p for p in quiz_files(cls.data, code, phase) if finished(p)]
            if done:
                raise QuizError(f"{code} has already finished the {phase}test ({os.path.basename(done[0])}).")
            warnings = []
            if phase == "post" and not any(finished(p) for p in quiz_files(cls.data, code, "pre")):
                warnings.append(f"{code} has no finished pretest on this laptop.")
            if phase == "pre" and quiz_files(cls.data, code, "post"):
                raise QuizError(f"{code} already has a posttest: the pretest cannot come after it.")
            unfinished = [p for p in glob.glob(str(cls.data / f"{code}_quiz-{phase}_*.csv")) if not finished(p)]
            if unfinished:
                warnings.append(f"An unfinished {phase}test exists ({os.path.basename(unfinished[0])}). "
                                "Starting restarts it from item 1 in the same file.")
            return {"ok": True, "code": code, "phase": phase, "warnings": warnings}
        if path == "/start":
            checked = self.handle_post("/check", body)
            code, phase = checked["code"], checked["phase"]
            if cls.quiz is not None:
                cls.quiz.log("quiz_abandoned", f"phase={cls.quiz.phase} seconds={cls.quiz.seconds():.1f}")
                cls.quiz.file.close()
            unfinished = [p for p in glob.glob(str(cls.data / f"{code}_quiz-{phase}_*.csv")) if not finished(p)]
            stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M")
            target = unfinished[0] if unfinished else str(cls.data / f"{code}_quiz-{phase}_{stamp}.csv")
            cls.quiz = Quiz(target, code, phase, restart=bool(unfinished))
            return {"ok": True, "file": os.path.basename(target)}
        quiz = cls.quiz
        if quiz is None:
            raise QuizError("No quiz is running.")
        item = body.get("item")
        if item is not None and item not in KEY:
            raise QuizError("Unknown item.")
        if path == "/event":
            kind = body.get("kind")
            if kind == "item":
                quiz.log("quiz_item", f"item={item} shown=1")
            elif kind == "choice":
                quiz.log("quiz_choice", f"item={item} choice={body['choice']}")
            elif kind == "answer":
                choice = body.get("choice") or ""
                if choice:
                    quiz.answers[item] = choice
                quiz.log("quiz_answer", f"item={item} choice={choice} correct={int(choice == KEY[item])} "
                                        f"seconds={float(body.get('seconds', 0)):.1f}")
            elif kind == "glossary":
                quiz.log("quiz_glossary", f"open={int(bool(body.get('open')))} item={item if item is not None else ''}")
            else:
                raise QuizError("Unknown event.")
            return {"ok": True}
        if path == "/finish":
            missing = [n for n in KEY if n not in quiz.answers]
            if missing:
                raise QuizError("Unanswered items: " + ", ".join(map(str, missing)))
            name = os.path.basename(quiz.path)
            quiz.end()
            cls.quiz = None
            return {"ok": True, "file": name}
        raise QuizError("Unknown request.")


class QuizError(Exception):
    pass


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--data", default=str(ROOT / "study_data" / "quiz"), help="folder for the quiz files")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()
    Handler.data = pathlib.Path(args.data).resolve()
    Handler.data.mkdir(parents=True, exist_ok=True)
    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    url = f"http://127.0.0.1:{args.port}/"
    print(f"QISCIT quiz at {url}  (data: {Handler.data})  Ctrl+C to stop")
    if not args.no_browser:
        threading.Timer(0.5, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        if Handler.quiz is not None:
            Handler.quiz.log("quiz_abandoned", f"phase={Handler.quiz.phase} seconds={Handler.quiz.seconds():.1f}")
            Handler.quiz.file.close()
        print("stopped")


if __name__ == "__main__":
    main()
