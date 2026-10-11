"""V5 M9c S0b: one compact table from the load probe's reports (probe-*.json), for PROBE.md.

Usage: python plan/v5/phase-09c-multiship/s0b/summarize.py <probe.json> [<probe.json> ...]
"""
import json
import sys


def stat(s, *keys):
    return "/".join(f"{s.get(k, 0):,.0f}" if s.get(k, 0) >= 100 else f"{s.get(k, 0):.2f}" for k in keys)


def main(paths):
    for path in paths:
        d = json.load(open(path, encoding="utf-8-sig"))
        secs = d["measuredSeconds"]
        print(f"== {path}")
        print(f"preset {d['preset']} backend {d['scriptingBackend']} editor {d['editor']} measured {secs:.0f} s, snapshots {d['snapshots']}, "
              f"kills {d['kills']} ({d['kills'] / max(1, secs):.1f}/s), level {d['level']}, active enemies p50/min "
              f"{d['activeEnemies']['p50']:.0f}, spawn failures {d['spawnFailures']}, clamps {d['quantizeClamps']}")
        t = d["serverTickMs"]
        print(f"server tick ms p50/p95/p99/max(window) {t['p50']:.2f}/{t['p95']:.2f}/{t['p99']:.2f}/{t['max']:.2f}, voyage max {d['serverTickMsMax']:.2f}; "
              f"probe in tick p50/p95 {d['probeTickMs']['p50']:.2f}/{d['probeTickMs']['p95']:.2f}; primary encode/snapshot p50/p95 "
              f"{d['primaryEncodeMsPerSnapshot']['p50']:.2f}/{d['primaryEncodeMsPerSnapshot']['p95']:.2f}")
        for c in d["realClient"]:
            print(f"real client (v5 protocol): down {c['downPayloadPerSecond']:,.0f} B/s, up {c['upPayloadPerSecond']:,.0f} B/s payload, "
                  f"up wire with a 2 B ack {c['upWireWithAckPerSecond']:,.0f} B/s")
        print("fx calls/s: " + ", ".join(f"{k} {v:.1f}" for k, v in sorted(d["fxCallsPerSecond"].items(), key=lambda kv: -kv[1])))
        for b in d["budgets"]:
            print(f"-- budget {b['budgetWireBytesPerSnapshot']} B/snapshot (in tick: {b['inTick']})")
            print("   client  | wire B/s p50/p95/max | payload p50 | own | ships | enemies p50/p95 | fxOwn | fxOthers | threats | framing | "
                  "view p50 | near age ms p95/max | close age p95/max | own fx delay p95 | near stale | fx dropped | frames over")
            for c in b["clients"]:
                print(f"   {c['ship']:7} | {stat(c['wireBytesPerSecond'], 'p50', 'p95', 'max')} | {c['payloadBytesPerSecond']['p50']:,.0f} | "
                      f"{c['ownBytesPerSecond']['p50']:,.0f} | {c['shipsBytesPerSecond']['p50']:,.0f} | {stat(c['enemiesBytesPerSecond'], 'p50', 'p95')} | "
                      f"{c['fxOwnBytesPerSecond']['p50']:,.0f} | {c['fxOthersBytesPerSecond']['p50']:,.0f} | {c['threatsBytesPerSecond']['p50']:,.0f} | "
                      f"{c['framingBytesPerSecond']['p50']:,.0f} | {c['enemiesInView']['p50']:.0f} | "
                      f"{c['nearEnemyUpdateAgeMs']['p95']:.0f}/{c['nearEnemyUpdateAgeMs']['max']:.0f} | "
                      f"{c['closeEnemyUpdateAgeMs']['p95']:.0f}/{c['closeEnemyUpdateAgeMs']['max']:.0f} | {c['ownFxDelayMs']['p95']:.0f} | "
                      f"{c['nearStaleShare']:.3f} | {c['fxDropped']} | {c['framesOverBudget']}")


if __name__ == "__main__":
    main(sys.argv[1:])
