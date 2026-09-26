"""Exact trace comparison outside Unity, same rules as TraceComparer.CompareExact:
key header fields must agree, and every non-header line must match with frame numbers ("f") ignored.
Usage: python compare-traces.py [--ignore-preset] expected.jsonl actual.jsonl [more pairs...]   (exit 1 if any pair differs)
--ignore-preset: for V3-parity runs, where V4 runs with presets/v3-parity.json (restores V3 values that V4 changed
by decision, e.g. band 0 = 0.6/s) against V3 goldens that have no preset.
"""
import json
import re
import sys

HEADER_KEYS = ["seed", "pilot", "dt", "captain", "platform", "screen", "bossAt", "killBossAt", "killArenaAfter", "configHash", "visualPins", "preset", "invulnerable"]
FRAME = re.compile(r',"f":-?\d+')


def load(path):
    lines = [l.rstrip("\n") for l in open(path, encoding="utf-8") if l.strip()]
    header = next((json.loads(l) for l in lines if '"type":"header"' in l), {})
    events = [FRAME.sub("", l) for l in lines if '"type":"header"' not in l]
    return header, events


IGNORED = set()


def compare(a_path, b_path):
    ha, ea = load(a_path)
    hb, eb = load(b_path)
    defaults = {"preset": "", "killArenaAfter": -1, "invulnerable": False}  # fields that older traces do not have
    get = lambda h, k: h.get(k, defaults.get(k, "(missing)"))
    diffs = [f"header {k}: {get(ha, k)} vs {get(hb, k)}" for k in HEADER_KEYS if k not in IGNORED and get(ha, k) != get(hb, k)]
    for i, (x, y) in enumerate(zip(ea, eb)):
        if x != y:
            diffs.append(f"line {i}: {x[:140]}\n        vs {y[:140]}")
            break
    if len(ea) != len(eb):
        diffs.append(f"length {len(ea)} vs {len(eb)}")
    return diffs, len(ea)


if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0] == "--ignore-preset":
        IGNORED.add("preset")
        args = args[1:]
    failed = False
    for a, b in zip(args[0::2], args[1::2]):
        diffs, n = compare(a, b)
        name = f"{a.split('/')[-1]} vs {b.split('/')[-1]}"
        print(f"{name}: {'MATCH' if not diffs else 'DIFF'} ({n} lines)")
        for d in diffs[:4]:
            print("   ", d)
        failed |= bool(diffs)
    sys.exit(1 if failed else 0)
