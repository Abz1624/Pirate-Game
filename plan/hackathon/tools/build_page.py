"""Builds the hackathon tracker page from plan/hackathon/status.json. Usage: python build_page.py <out.html>"""
import json, sys, html, os

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
d = json.load(open(os.path.join(root, 'status.json'), encoding='utf-8'))
e = html.escape
LABEL = {'done': 'Done', 'building': 'Building', 'todo': 'To do', 'pending': 'Not yet', 'n/a': '-',
         'blocked': 'Blocked', 'testing': 'Testing'}


def chip(s):
    return '<span class="chip s-%s">%s</span>' % (e(s.replace('/', '')), e(LABEL.get(s, s)))


def frac(f):
    keys = [k for k in ('server', 'unity', 'chain') if f[k] != 'n/a']
    return sum(1 for k in keys if f[k] == 'done'), len(keys)


done = sum(frac(f)[0] for f in d['features'])
total = sum(frac(f)[1] for f in d['features'])
pct = round(100 * done / total) if total else 0
rows = ''.join(
    '<tr><th scope="row"><span class="fname">%s</span><span class="fdet">%s</span></th><td>%s</td><td>%s</td><td>%s</td></tr>'
    % (e(f['name']), e(f['detail']), chip(f['server']), chip(f['unity']), chip(f['chain'])) for f in d['features'])
sched = ''.join('<li><span class="t">%s</span><span>%s</span></li>' % (e(t), e(x)) for t, x in d['schedule'])
needs = ''.join('<li class="%s"><span class="box">%s</span><span>%s</span></li>'
                % ('ok' if n['done'] else 'open', '&#10003;' if n['done'] else '', e(n['text'])) for n in d['needs'])
wal = ''.join('<li><span class="wl">%s</span><code>%s</code></li>' % (e(a), e(b)) for a, b in d['wallets'])
log = ''.join('<li><span class="t">%s</span><span>%s</span></li>' % (e(t), e(x)) for t, x in reversed(d['log']))

css = """
/* Layout: one working column: header with progress, the feature matrix, then two lists side by side, then the log. */
:root { --bg:#f3f6f7; --panel:#ffffff; --fg:#14232b; --muted:#5b6b74; --line:#d8e1e5; --accent:#0d7a8a; --good:#1f8a4c; --warn:#b26a00; --idle:#7c8b93; --chipbg:#eef3f5;
  --display:"Bricolage Grotesque", "Segoe UI", system-ui, sans-serif; --body:"IBM Plex Sans", "Segoe UI", system-ui, sans-serif; --mono:"IBM Plex Mono", ui-monospace, Consolas, monospace; color-scheme:light }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { --bg:#0e171b; --panel:#15222a; --fg:#e4edf0; --muted:#97a8b0; --line:#25343c; --accent:#3fc1d2; --good:#4cc47e; --warn:#e3a33b; --idle:#7f8f97; --chipbg:#1c2b33; color-scheme:dark } }
:root[data-theme="dark"] { --bg:#0e171b; --panel:#15222a; --fg:#e4edf0; --muted:#97a8b0; --line:#25343c; --accent:#3fc1d2; --good:#4cc47e; --warn:#e3a33b; --idle:#7f8f97; --chipbg:#1c2b33; color-scheme:dark }
body { background:var(--bg); color:var(--fg); font-family:var(--body); line-height:1.5 }
.wrap { max-width:1040px; margin:0 auto; padding-inline:16px; padding-block:24px 48px; display:grid; gap:20px }
h1 { font-family:var(--display); font-size:clamp(1.6rem,4vw,2.3rem); margin:0; text-wrap:balance; letter-spacing:-.01em }
h2 { font-family:var(--display); font-size:1.1rem; margin:0 0 10px }
.meta { color:var(--muted); font-size:.9rem; display:flex; flex-wrap:wrap; gap:6px 18px; font-variant-numeric:tabular-nums }
.head { display:grid; gap:10px }
.headline { font-size:1.05rem; max-width:65ch; margin:0 }
.bar { height:10px; background:var(--chipbg); border-radius:6px; overflow:hidden }
.bar > i { display:block; height:100%; background:var(--accent) }
.panel { background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:16px; min-width:0 }
.tablewrap { overflow-x:auto }
table { width:100%; border-collapse:collapse; min-width:560px }
th, td { text-align:left; padding:10px 8px; border-top:1px solid var(--line); vertical-align:top }
thead th { border-top:none; font-size:.75rem; text-transform:uppercase; letter-spacing:.06em; color:var(--muted) }
.fname { display:block; font-weight:600 }
.fdet { display:block; color:var(--muted); font-size:.85rem; font-weight:400; max-width:56ch }
.chip { display:inline-block; font-size:.78rem; padding:2px 9px; border-radius:999px; background:var(--chipbg); color:var(--idle); white-space:nowrap; font-weight:600 }
.s-done { color:var(--good) } .s-building, .s-testing { color:var(--accent) } .s-blocked { color:var(--warn) }
.two { display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:20px }
ul { list-style:none; margin:0; padding:0; display:grid; gap:8px }
li { display:flex; gap:10px; min-width:0 }
li > span:last-child, li > code { min-width:0 }
.t { font-family:var(--mono); font-size:.82rem; color:var(--muted); min-width:7.5em; flex:none; font-variant-numeric:tabular-nums }
.box { width:1.1em; height:1.1em; border:1.5px solid var(--line); border-radius:4px; display:inline-grid; place-items:center; color:var(--good); flex:none; font-size:.8rem }
.ok { color:var(--muted) } .open .box { border-color:var(--warn) }
.wl { color:var(--muted); min-width:9em; flex:none }
code { font-family:var(--mono); font-size:.8rem; word-break:break-all }
"""

page = ('<title>Rogue Pirates Hackathon</title>\n<style>' + css + '</style>\n'
        '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:wght@600;700&family=IBM+Plex+Mono&family=IBM+Plex+Sans:wght@400;600&display=swap">\n'
        '<div class="wrap">\n<header class="head">\n<h1>Rogue Pirates: hackathon build</h1>\n'
        '<div class="meta"><span>Deadline %s</span><span>Updated %s</span><span>Solana devnet</span><span>Branch hackathon-demo</span></div>\n'
        '<p class="headline">%s</p>\n'
        '<div class="bar" role="img" aria-label="%d%% of feature parts done"><i style="width:%d%%"></i></div>\n'
        '<div class="meta"><span>%d of %d feature parts done (%d%%)</span></div>\n</header>\n'
        '<section class="panel"><h2>Features</h2><div class="tablewrap"><table><thead><tr><th>Feature</th><th>Server</th><th>Game</th><th>On devnet</th></tr></thead><tbody>%s</tbody></table></div></section>\n'
        '<div class="two">\n<section class="panel"><h2>Schedule (IST)</h2><ul>%s</ul></section>\n'
        '<section class="panel"><h2>Needed from you</h2><ul>%s</ul><h2 style="margin-top:16px">Devnet wallets</h2><ul>%s</ul></section>\n</div>\n'
        '<section class="panel"><h2>Log</h2><ul>%s</ul></section>\n</div>\n'
        % (e(d['deadline']), e(d['updated']), e(d['headline']), pct, pct, done, total, pct, rows, sched, needs, wal, log))
open(sys.argv[1], 'w', encoding='utf-8').write(page)
print('wrote', sys.argv[1], len(page), 'bytes;', '%d%%' % pct)
