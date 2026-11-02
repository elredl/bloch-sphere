#!/usr/bin/env python3
"""Appends each participant's quiz rows (tools/quiz.py) to their session file.

    python3 tools/merge_quiz.py FOLDER [--quiz study_data/quiz]

FOLDER holds session files (P07_C3_20261020-1432.csv), e.g. the study_data/<date-time>/
folder pull_data just made, or the folder the desktop P*_C1_*.csv files were copied to.
For every session file, the participant's finished pretest and posttest files in --quiz are
appended to it (rows unchanged except condition and stratum, copied from the session file),
then the quiz files move to FOLDER/quiz/ so the original record stays with the session and
is never merged twice. Unfinished quiz files are skipped and reported. Running it again is
safe: a quiz whose quiz_start row is already in the session file is not appended again.
Analysis: sort a merged file by utc; the quiz rows have blank session_s and lesson_s.
"""
import argparse
import csv
import pathlib
import re
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SESSION = re.compile(r"^(P\d{2})_C[123]_\d{8}-\d{4}\.csv$")
QUIZ = re.compile(r"^(P\d{2})_quiz-(pre|post)_\d{8}-\d{4}\.csv$")


def rows(path):
    with open(path, encoding="utf-8", newline="") as f:
        return list(csv.reader(f))


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("folder")
    parser.add_argument("--quiz", default=str(ROOT / "study_data" / "quiz"))
    args = parser.parse_args()
    folder, quiz_dir = pathlib.Path(args.folder), pathlib.Path(args.quiz)

    sessions = {}
    for path in sorted(folder.glob("P*_C*_*.csv")):
        m = SESSION.match(path.name)
        if m:
            sessions.setdefault(m.group(1), []).append(path)
    quizzes = {}
    for path in sorted(quiz_dir.glob("P*_quiz-*.csv")) if quiz_dir.is_dir() else []:
        m = QUIZ.match(path.name)
        if m:
            quizzes.setdefault(m.group(1), []).append((m.group(2), path))
    for found in quizzes.values():
        found.sort(key=lambda q: (q[0] != "pre", q[1].name))  # the pretest's rows first

    problems = 0
    for code, files in sessions.items():
        if len(files) > 1:
            print(f"  {code}: {len(files)} session files, quiz not merged (merge by hand)")
            problems += 1
            continue
        session = files[0]
        existing = rows(session)
        header = existing[0]
        col = {name: i for i, name in enumerate(header)}
        first = existing[1] if len(existing) > 1 else None
        starts = {(r[col["utc"]], r[col["event"]]) for r in existing[1:]}
        merged = []
        for phase, path in quizzes.get(code, []):
            quiz = rows(path)
            if quiz[0] != header:
                print(f"  {code}: {path.name} has other columns, skipped")
                problems += 1
                continue
            if not any(r[col["event"]] == "quiz_end" for r in quiz[1:]):
                print(f"  {code}: {path.name} is unfinished, not merged")
                problems += 1
                continue
            body = quiz[1:]
            if (body[0][col["utc"]], body[0][col["event"]]) not in starts:
                if first is not None:
                    for r in body:
                        r[col["condition"]], r[col["stratum"]] = first[col["condition"]], first[col["stratum"]]
                with open(session, "a", encoding="utf-8", newline="") as f:
                    csv.writer(f, lineterminator="\n").writerows(body)
            (folder / "quiz").mkdir(exist_ok=True)
            shutil.move(str(path), str(folder / "quiz" / path.name))
            merged.append(phase)
        present = {f.split("=", 1)[1] for r in rows(session)[1:] if r[col["event"]] == "quiz_end"
                   for f in r[col["fields"]].split() if f.startswith("phase=")}
        missing = [p for p in ("pre", "post") if p not in present]
        note = f"  {code}: {session.name} + quiz {', '.join(merged) or 'nothing new'}"
        if missing:
            note += f"  (MISSING {' and '.join(missing)}test)"
            problems += 1
        print(note)
    for code in sorted(set(quizzes) - set(sessions)):
        print(f"  {code}: quiz files but no session file in {folder} (left in {quiz_dir})")
    print(f"quiz merge: {len(sessions)} session files, {problems} to check")
    return 0


if __name__ == "__main__":
    sys.exit(main())
