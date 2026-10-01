"""V5 phone benchmark: one table per run from phone-bench.mjs's results.jsonl.

Usage: python plan/v5/tools/bench-report.py [plan/v5/perf/phone-bench/results.jsonl] > table.md
Each row is one variant: the early window (natural first-minute load) and the peak window (the clock at 3:35, or a fixed
crowd), frame rate, median / 95th / 99th percentile frame time, the share of frames over 33 ms, enemies alive, the
WebAssembly heap, Chrome's renderer and GPU process memory (PSS) and the battery temperature at the start and the peak.
"""
import json
import sys

path = sys.argv[1] if len(sys.argv) > 1 else 'plan/v5/perf/phone-bench/results.jsonl'
rows = [json.loads(line) for line in open(path, encoding='utf-8') if line.strip()]


def window(record, phase):
    return next((r for r in record.get('results', []) if r.get('phase') == phase), None)


def fmt(w):
    if not w:
        return '-', '-', '-'
    return (f"{w['fps']:.1f}", f"{w['p50']:.0f} / {w['p95']:.0f} / {w['p99']:.0f}", f"{w['over33']:.1f}%")


print('| # | Variant | Early fps | Early ms (p50/p95/p99) | Early >33 ms | Peak fps | Peak ms (p50/p95/p99) | Peak >33 ms | Enemies (peak avg) | Heap MB | Renderer MB | GPU proc MB | Battery °C (start, peak) | Load s |')
print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
for i, rec in enumerate(rows, 1):
    if rec.get('status') == 'error':
        print(f"| {i} | {rec.get('label')} | error: {rec.get('error', '')[:60]} |" + ' |' * 12)
        continue
    info, early, peak = window(rec, 'info'), window(rec, 'early'), window(rec, 'peak')
    e, p = fmt(early), fmt(peak)
    mem = rec.get('memory') or {}
    t = rec.get('temps') or {}
    status = '' if rec.get('status') == 'ok' else f" ({rec.get('status')}: {str(rec.get('failure') or '')[:40]})"
    enemies = f"{peak['enemiesAvg']:.0f}" if peak and peak.get('enemiesAvg', -1) >= 0 else '-'
    print(f"| {i} | {rec['label']}{status} | {e[0]} | {e[1]} | {e[2]} | {p[0]} | {p[1]} | {p[2]} | {enemies} | "
          f"{info['heapMB']:.0f} | {mem.get('rendererMB', '-')} | {mem.get('gpuProcessMB', '-')} | "
          f"{t.get('startC', '-')}, {t.get('peakC', '-')} | {info['loadSeconds']:.1f} |" if info else
          f"| {i} | {rec['label']}{status} |" + ' - |' * 12)
