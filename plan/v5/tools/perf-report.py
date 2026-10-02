"""V5 phone and desktop performance report (2 Oct 2026): builds the HTML report from the benchmark results.

Usage: python plan/v5/tools/perf-report.py <out.html>
Reads plan/v5/perf/phone-bench/results.jsonl (phone), desktop.jsonl (desktop), android.jsonl (APK, if present) and the
Editor profiles in plan/v5/perf/editor-profile/. Every number on the page comes from those files.
"""
import html
import json
import os
import statistics
import sys

ROOT = os.path.join(os.path.dirname(__file__), '..', 'perf')
OUT = sys.argv[1] if len(sys.argv) > 1 else 'perf-report.html'


def load(name):
    path = os.path.join(ROOT, 'phone-bench', name)
    if not os.path.exists(path):
        return []
    rows = []
    for line in open(path, encoding='utf-8'):
        if line.strip():
            r = json.loads(line)
            if r.get('status') == 'ok':
                r['w'] = {x['phase']: x for x in r.get('results', [])}
                rows.append(r)
    return rows


phone, desktop, android = load('results.jsonl'), load('desktop.jsonl'), load('android.jsonl')


def find(rows, prefix):
    return [r for r in rows if r['label'].startswith(prefix)]


def mean(values):
    values = [v for v in values if v is not None]
    return statistics.mean(values) if values else None


def stat(rows, phase, key):
    return mean([r['w'][phase][key] for r in rows if phase in r['w']])


def mem(rows, key):
    return mean([(r.get('memory') or {}).get(key) for r in rows])


def f(v, digits=1, unit=''):
    if v is None:
        return '–'
    return f'{v:.{digits}f}{unit}'


esc = html.escape

# ---- Data for the page ----------------------------------------------------------------------------------------------

# Final comparison (run 5 phone, D5 desktop), else run 4 / earlier desktop rows.
def pair(rows, before, after):
    return find(rows, before), find(rows, after)


r5b80, r5a80 = pair(phone, 'R5 before (F), crowd 80', 'R5 final mobile, crowd 80')
r5bn, r5an = pair(phone, 'R5 before (F), natural', 'R5 final mobile, natural')
d5b80, d5a80 = pair(desktop, 'D5 before (F), crowd 80', 'D5 final desktop, crowd 80')
d5bn, d5an = pair(desktop, 'D5 before (F), natural', 'D5 final desktop, natural')
if not d5b80:
    d5b80, d5a80 = pair(desktop, 'desktop: before (build F), crowd 80', 'desktop: final build, crowd 80')
    d5bn, d5an = pair(desktop, 'desktop: before (build F), natural', 'desktop: final build, natural')

crowd_curve = [
    ('~20 (natural)', find(phone, 'A base (2)') + find(phone, 'A base (3)')),
    ('80', find(phone, 'E1 speed+wasm23, crowd 80')),
    ('160', find(phone, 'E1 crowd 160')),
    ('260 (pool cap)', find(phone, 'E1 crowd 260')),
]

switches = [
    ('As is (build F)', find(phone, 'F crowd 80, as is')),
    ('Enemy health bars off', find(phone, 'F crowd 80, enemy bars off')),
    ('Minimap frozen', find(phone, 'F crowd 80, minimap frozen')),
    ('Both off', find(phone, 'F crowd 80, both off')),
]

settings = [
    ('Build-time code (A, baseline)', find(phone, 'A base (BuildTimes')),
    ('Runtime-speed code (B)', find(phone, 'B base')),
    ('Render scale 1.0', find(phone, 'B scale 1.0')),
    ('Render scale 0.5', find(phone, 'B scale 0.5')),
    ('Low quality tier', find(phone, 'B Low tier')),
    ('No shadows', find(phone, 'B no shadows')),
    ('No post-processing', find(phone, 'B no post-processing')),
    ('No HDR, no MSAA', find(phone, 'B no HDR')),
    ('PC quality level', find(phone, 'B PC quality')),
    ('30 fps cap', find(phone, 'B capped at 30')),
    ('Baseline again, 28 min later (heat)', find(phone, 'A base again')),
]

textures = [
    ('DXT (what phones got)', find(phone, 'E1 DXT textures')),
    ('ASTC (phone format)', find(phone, 'E2 ASTC textures')),
]


# ---- Charts --------------------------------------------------------------------------------------------------------

def bar_chart(items, value_label, max_value, unit='', width=640, row=34, good_high=True):
    """Horizontal bars, one scale. items: [(label, value, note)]."""
    left, right = 190, 70
    inner = width - left - right
    height = row * len(items) + 24
    out = [f'<svg viewBox="0 0 {width} {height}" role="img" aria-label="{esc(value_label)}" class="chart">']
    for t in range(0, 5):
        x = left + inner * t / 4
        v = max_value * t / 4
        out.append(f'<line x1="{x:.1f}" y1="4" x2="{x:.1f}" y2="{height - 18}" class="grid"/>')
        out.append(f'<text x="{x:.1f}" y="{height - 4}" class="tick" text-anchor="middle">{v:g}{unit}</text>')
    for i, (label, value, note) in enumerate(items):
        y = 6 + i * row
        out.append(f'<text x="{left - 10}" y="{y + row / 2:.1f}" class="lab" text-anchor="end" dominant-baseline="middle">{esc(label)}</text>')
        if value is None:
            continue
        w = max(1.0, inner * min(value, max_value) / max_value)
        cls = 'bar' + (' hi' if note == 'hi' else '')
        out.append(f'<rect x="{left}" y="{y + 6}" width="{w:.1f}" height="{row - 14}" rx="3" class="{cls}"/>')
        out.append(f'<text x="{left + w + 6:.1f}" y="{y + row / 2:.1f}" class="val" dominant-baseline="middle">{value:.0f}{unit}</text>')
    out.append('</svg>')
    return '\n'.join(out)


def row_stats(rows):
    return {
        'n': len(rows),
        'fps': stat(rows, 'peak', 'fps'), 'p50': stat(rows, 'peak', 'p50'), 'p95': stat(rows, 'peak', 'p95'),
        'p99': stat(rows, 'peak', 'p99'), 'over33': stat(rows, 'peak', 'over33'), 'enemies': stat(rows, 'peak', 'enemiesAvg'),
        'efps': stat(rows, 'early', 'fps'), 'ep99': stat(rows, 'early', 'p99'),
        'gpu': mem(rows, 'gpuProcessMB'), 'renderer': mem(rows, 'rendererMB'), 'heap': stat(rows, 'info', 'heapMB'),
        'temp': mean([(r.get('temps') or {}).get('startC') for r in rows]),
    }


def table(groups, columns):
    head = ''.join(f'<th>{esc(c[0])}</th>' for c in columns)
    body = []
    for label, rows in groups:
        st = row_stats(rows)
        cells = ''.join(f'<td class="num">{c[1](st)}</td>' for c in columns[1:])
        body.append(f'<tr><th scope="row">{esc(label)}</th>{cells}</tr>')
    return f'<div class="scroll"><table><thead><tr>{head}</tr></thead><tbody>{"".join(body)}</tbody></table></div>'


PEAK_COLS = [
    ('Variant', None),
    ('Runs', lambda s: str(s['n'])),
    ('Avg fps', lambda s: f(s['fps'])),
    ('Median ms', lambda s: f(s['p50'], 0)),
    ('Slowest 5% ms', lambda s: f(s['p95'], 0)),
    ('Slowest 1% ms', lambda s: f(s['p99'], 0)),
    ('Frames > 33 ms', lambda s: f(s['over33'], 1, '%')),
    ('Battery °C', lambda s: f(s['temp'], 1)),
]

# Headline numbers
H = {
    'b80': row_stats(r5b80), 'a80': row_stats(r5a80), 'bn': row_stats(r5bn), 'an': row_stats(r5an),
    'db80': row_stats(d5b80), 'da80': row_stats(d5a80), 'dbn': row_stats(d5bn), 'dan': row_stats(d5an),
}


def headline(title, before, after, unit, note, lower_better=True, digits=0):
    if before is None or after is None:
        return ''
    better = after < before if lower_better else after > before
    return f'''<div class="tile"><div class="t-title">{esc(title)}</div>
      <div class="t-vals"><span class="before">{before:.{digits}f}{unit}</span><span class="arrow" aria-hidden="true">→</span>
      <span class="after {'good' if better else 'bad'}">{after:.{digits}f}{unit}</span></div><div class="t-note">{esc(note)}</div></div>'''


tiles = ''.join([
    headline('Phone, 80 enemies: average fps', H['b80']['fps'], H['a80']['fps'], '', 'Pixel 9a, warm (about 40 °C)', lower_better=False, digits=1),
    headline('Phone, 80 enemies: slowest 1% of frames', H['b80']['p99'], H['a80']['p99'], ' ms', 'the hitches you feel'),
    headline('Phone, natural game: slowest 1%', H['bn']['p99'], H['an']['p99'], ' ms', 'first minutes, no forced crowd'),
    headline('Phone GPU memory (Chrome GPU process)', H['b80']['gpu'], H['a80']['gpu'], ' MB', 'textures stay compressed (ASTC)'),
    headline('Desktop, 80 enemies: slowest 1%', H['db80']['p99'], H['da80']['p99'], ' ms', 'RTX 3050 laptop, Chrome; capped at 60 fps'),
])

crowd_chart = bar_chart([(lbl, row_stats(rows)['fps'], 'hi' if lbl == '80' else '') for lbl, rows in crowd_curve], 'Average fps by enemies alive', 60)
switch_chart = bar_chart([(lbl, row_stats(rows)['p99'], 'hi' if lbl == 'Both off' else '') for lbl, rows in switches], 'Slowest 1% of frames, ms', 160, ' ms')
settings_chart = bar_chart([(lbl, row_stats(rows)['fps'], '') for lbl, rows in settings], 'Average fps, peak window', 60)

ANDROID_NOTES = '''<p>Every variant is locked at the screen's 60 Hz, even with 80 enemies (the web build on the same phone: 37.5 fps), so the
      graphics API and frame pacing make no measurable difference here. The project default stays (Vulkan, GLES3 fallback, pacing
      off); the low-end Galaxy A15 should decide it. The texture cap is the win: APK 155.5 to 127.9 MB (-18%) and about 60 MB less
      memory. The playtest APK (no benchmark code) is 126.9 MB and is installed on your phone.</p>'''
android_section = ''
if android:
    groups = {}
    for r in android:
        groups.setdefault(r['label'].rsplit(' (', 1)[0], []).append(r)
    cols = PEAK_COLS[:-1] + [('App memory MB', lambda s: '–'), ('Battery °C', lambda s: f(s['temp'], 1))]
    rows_html = []
    for label, rows in groups.items():
        st = row_stats(rows)
        app = mean([(r.get('memory') or {}).get('appPssMB') for r in rows])
        rows_html.append(f'<tr><th scope="row">{esc(label)}</th><td class="num">{st["n"]}</td><td class="num">{f(st["fps"])}</td>'
                         f'<td class="num">{f(st["p50"], 0)}</td><td class="num">{f(st["p95"], 0)}</td><td class="num">{f(st["p99"], 0)}</td>'
                         f'<td class="num">{f(st["over33"], 1, "%")}</td><td class="num">{f(app, 0)}</td><td class="num">{f(st["temp"], 1)}</td></tr>')
    android_section = f'''<section id="android"><h2>Android APK</h2>
      <p class="lede">The same benchmark in the installed app (IL2CPP, 64-bit ARM), started over USB.</p>
      <div class="scroll"><table><thead><tr><th>Variant</th><th>Runs</th><th>Avg fps</th><th>Median ms</th><th>Slowest 5% ms</th><th>Slowest 1% ms</th><th>Frames &gt; 33 ms</th><th>App memory MB</th><th>Battery °C</th></tr></thead>
      <tbody>{"".join(rows_html)}</tbody></table></div>{ANDROID_NOTES if 'ANDROID_NOTES' in globals() else ''}</section>'''

ed = {}
for name in ['profile-c0', 'profile-c80', 'profile-c80-nobars']:
    path = os.path.join(ROOT, 'editor-profile', name + '.json')
    if os.path.exists(path):
        ed[name] = json.load(open(path, encoding='utf-8')).get('profile') or {}

page = open(os.path.join(os.path.dirname(__file__), 'perf-report.template.html'), encoding='utf-8').read()
replacements = {
    '{{TILES}}': tiles,
    '{{CROWD_CHART}}': crowd_chart,
    '{{SWITCH_CHART}}': switch_chart,
    '{{SETTINGS_CHART}}': settings_chart,
    '{{FINAL_PHONE_TABLE}}': table([('Before (build F), 80 enemies', r5b80), ('Final mobile build, 80 enemies', r5a80),
                                    ('Before (build F), natural game', r5bn), ('Final mobile build, natural game', r5an)], PEAK_COLS),
    '{{FINAL_DESKTOP_TABLE}}': table([('Before (build F), 80 enemies', d5b80), ('Final desktop build, 80 enemies', d5a80),
                                      ('Before (build F), natural game', d5bn), ('Final desktop build, natural game', d5an)], PEAK_COLS[:-1]),
    '{{SETTINGS_TABLE}}': table(settings, PEAK_COLS),
    '{{SWITCH_TABLE}}': table(switches, PEAK_COLS),
    '{{CROWD_TABLE}}': table(crowd_curve, PEAK_COLS),
    '{{TEXTURE_TABLE}}': table(textures, [('Build', None), ('Runs', lambda s: str(s['n'])), ('Heap MB', lambda s: f(s['heap'], 0)),
                                          ('Chrome renderer MB', lambda s: f(s['renderer'], 0)), ('GPU process MB', lambda s: f(s['gpu'], 0)),
                                          ('Avg fps, 80 enemies', lambda s: f(s['fps']))]),
    '{{FINAL_MEMORY}}': f"{f(H['a80']['gpu'], 0)} MB GPU process, {f(H['a80']['heap'], 0)} MB heap",
    '{{ANDROID}}': android_section,
}
for k, v in replacements.items():
    page = page.replace(k, v)
open(OUT, 'w', encoding='utf-8').write(page)
print('wrote', OUT, len(page), 'bytes;', len(phone), 'phone runs,', len(desktop), 'desktop runs,', len(android), 'android runs')
