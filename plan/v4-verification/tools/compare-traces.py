"""Exact trace comparison outside Unity, same rules as TraceComparer.CompareExact:
key header fields must agree, and every non-header line must match with frame numbers ("f") ignored.
Usage: python compare-traces.py expected.jsonl actual.jsonl [more pairs...]   (exit 1 if any pair differs)
"""
import json
import re
import sys

HEADER_KEYS = ["seed", "pilot", "dt", "captain", "platform", "screen", "bossAt", "killBossAt", "configHash", "visualPins"]
FRAME = re.compile(r',"f":-?\d+')


def load(path):
    lines = [l.rstrip("\n") for l in open(path, encoding="utf-8") if l.strip()]
    header = next((json.loads(l) for l in lines if '"type":"header"' in l), {})
    events = [FRAME.sub("", l) for l in lines if '"type":"header"' not in l]
    return header, events


def compare(a_path, b_path):
    ha, ea = load(a_path)
    hb, eb = load(b_path)
    diffs = [f"header {k}: {ha.get(k, '(missing)')} vs {hb.get(k, '(missing)')}"
             for k in HEADER_KEYS if ha.get(k, "(missing)") != hb.get(k, "(missing)")]
    for i, (x, y) in enumerate(zip(ea, eb)):
        if x != y:
            diffs.append(f"line {i}: {x[:140]}\n        vs {y[:140]}")
            break
    if len(ea) != len(eb):
        diffs.append(f"length {len(ea)} vs {len(eb)}")
    return diffs, len(ea)


if __name__ == "__main__":
    args = sys.argv[1:]
    failed = False
    for a, b in zip(args[0::2], args[1::2]):
        diffs, n = compare(a, b)
        name = f"{a.split('/')[-1]} vs {b.split('/')[-1]}"
        print(f"{name}: {'MATCH' if not diffs else 'DIFF'} ({n} lines)")
        for d in diffs[:4]:
            print("   ", d)
        failed |= bool(diffs)
    sys.exit(1 if failed else 0)
