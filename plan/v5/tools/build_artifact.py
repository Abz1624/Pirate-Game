"""Builds the "Rogue Pirates V5 Plan" tracking page from plan/v5/V5_PLAN.md and plan/v5/status.json.

Usage (repo root):  python plan/v5/tools/build_artifact.py <output.html>
The plan file is the agents' source of truth; status.json holds what changes as work happens (progress log, phase
states, proof of tests, review findings, TODOs). Re-run after either changes, then republish the page.
"""
import html
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PLAN = ROOT / "plan" / "v5" / "V5_PLAN.md"
STATUS = ROOT / "plan" / "v5" / "status.json"
ANSWERS = ROOT / "plan" / "v5" / "ANSWERS.md"
PERSONAS = ROOT / "plan" / "v5" / "PLAYER_PERSONALITIES.md"
STYLE = ROOT / "plan" / "v5" / "tools" / "v4_style.html"


# ---------- markdown helpers ----------

def inline(text):
    """Escape, then render `code`, **bold**, [text](url). Content inside code is left alone."""
    out, pos = [], 0
    for m in re.finditer(r"`([^`]+)`", text):
        out.append(_inline_plain(text[pos:m.start()]))
        out.append("<code>" + html.escape(m.group(1)) + "</code>")
        pos = m.end()
    out.append(_inline_plain(text[pos:]))
    return "".join(out)


def _inline_plain(t):
    t = html.escape(t, quote=False)
    t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", lambda m: f'<a href="{m.group(2)}">{m.group(1)}</a>', t)
    return t


def split_sections(md, level):
    """Split markdown into (title, body) by headings of exactly `level` hashes."""
    pat = re.compile(r"^" + "#" * level + r" (.+)$", re.M)
    parts, last, title = [], 0, None
    for m in pat.finditer(md):
        if title is not None:
            parts.append((title, md[last:m.start()]))
        title, last = m.group(1).strip(), m.end()
    if title is not None:
        parts.append((title, md[last:]))
    return parts


def blocks(body):
    """Yield ('p', text) | ('ul', [items]) | ('ol', [items]) | ('table', rows) | ('code', text)."""
    lines = body.strip("\n").split("\n")
    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        if not line.strip() or line.strip() == "---":
            i += 1
            continue
        if line.startswith("```"):
            j = i + 1
            buf = []
            while j < n and not lines[j].startswith("```"):
                buf.append(lines[j]); j += 1
            yield ("code", "\n".join(buf)); i = j + 1; continue
        if line.startswith("|"):
            rows = []
            while i < n and lines[i].startswith("|"):
                cells = [c.strip() for c in lines[i].strip().strip("|").split("|")]
                if not all(re.fullmatch(r":?-{3,}:?", c) for c in cells):
                    rows.append(cells)
                i += 1
            yield ("table", rows); continue
        m_ul, m_ol = re.match(r"^- (.*)", line), re.match(r"^\d+\. (.*)", line)
        if m_ul or m_ol:
            kind = "ul" if m_ul else "ol"
            items = []
            while i < n:
                l = lines[i]
                m = re.match(r"^- (.*)", l) if kind == "ul" else re.match(r"^\d+\. (.*)", l)
                if m:
                    items.append(m.group(1)); i += 1
                elif l.startswith("  ") and l.strip() and items:
                    items[-1] += " " + l.strip(); i += 1
                else:
                    break
            yield (kind, items); continue
        buf = [line.strip()]
        i += 1
        while i < n and lines[i].strip() and not re.match(r"^(- |\d+\. |\||```|#)", lines[i]):
            buf.append(lines[i].strip()); i += 1
        yield ("p", " ".join(buf))


def table_html(rows, cls=""):
    head, body = rows[0], rows[1:]
    h = "".join(f"<th>{inline(c)}</th>" for c in head)
    b = "".join("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in r) + "</tr>" for r in body)
    return f'<div class="tbl-wrap"><table class="{cls}"><thead><tr>{h}</tr></thead><tbody>{b}</tbody></table></div>'


def generic_html(body):
    out = []
    for kind, val in blocks(body):
        if kind == "p":
            out.append(f"<p>{inline(val)}</p>")
        elif kind in ("ul", "ol"):
            items = "".join(f"<li>{inline(x)}</li>" for x in val)
            out.append(f'<{kind} class="tight">{items}</{kind}>')
        elif kind == "table":
            out.append(table_html(val))
        elif kind == "code":
            out.append(f"<pre><code>{html.escape(val)}</code></pre>")
    return "\n".join(out)


# ---------- phases ----------

TASK_RE = re.compile(r"^([ICDGNT])(\d+[a-z]?\.[\w.x]+)\s+(.*)$")
CHECK_LABELS = ["Automated check:", "Owner playtest:", "Owner check:", "Done when:"]
STATE_CHIP = {
    "done": ("ok", "passed"), "playtest": ("moved", "awaiting your playtest"), "review": ("sea", "in review"),
    "building": ("sea", "in progress"), "next": ("moved", "next"), "planned": ("", "planned"),
}


def parse_phases(sec5):
    stages = []
    for stitle, sbody in split_sections(sec5, 3):
        pre = sbody.split("\n####")[0]
        phases = []
        for ptitle, pbody in split_sections(sbody, 4):
            m = re.match(r"(M\d+[a-z]?) — (.+)", ptitle)
            code, name = (m.group(1), m.group(2)) if m else ("", ptitle)
            goal, notes, tasks, checks = "", [], [], []
            for kind, val in blocks(pbody):
                if kind == "p":
                    labels = "|".join(re.escape(x) for x in CHECK_LABELS)
                    pieces = re.split(f"({labels})", val)
                    lead = pieces[0].strip()
                    if lead.startswith("Goal:"):
                        goal = lead[5:].strip()
                    elif lead:
                        notes.append(lead)
                    for k in range(1, len(pieces), 2):
                        checks.append((pieces[k].rstrip(":"), pieces[k + 1].strip()))
                elif kind == "ul":
                    for item in val:
                        tm = TASK_RE.match(item)
                        tasks.append((tm.group(1), tm.group(1) + tm.group(2), tm.group(3)) if tm else ("", "·", item))
                else:
                    notes.append(("raw", kind, val))
            phases.append(dict(code=code, name=name, goal=goal, notes=notes, tasks=tasks, checks=checks))
        stages.append(dict(title=stitle, pre=pre, phases=phases))
    return stages


def phase_html(p, status):
    st = status["phases"].get(p["code"], {})
    state = st.get("state", "planned")
    cls, label = STATE_CHIP.get(state, ("", state))
    chip = st.get("chip", label)
    tasks = "".join(
        f'<li><span class="tid {k.lower()}">{html.escape(tid)}</span><div>{inline(t)}</div></li>'
        for k, tid, t in p["tasks"])
    notes = "".join(f'<p class="muted" style="font-size:14px">{inline(n)}</p>' for n in p["notes"] if isinstance(n, str))
    checks = ""
    for label_, text in p["checks"]:
        c = "check you" if label_.startswith("Owner") else ("check done" if label_ == "Done when" else "check")
        title = {"Owner playtest": "Your playtest", "Owner check": "Your check"}.get(label_, label_)
        checks += f'<div class="{c}"><h4>{html.escape(title)}</h4><p>{inline(text)}</p></div>'
    results = status.get("phaseResults", {}).get(p["code"], [])
    res = ""
    if results:
        res = '<h4>Results</h4><div class="results">' + "".join(
            f'<div class="check done"><h4>{html.escape(r["title"])}</h4><p>{r["text"]}</p></div>' for r in results) + "</div>"
    goal = f'<p class="goal">{inline(p["goal"])}</p>' if p["goal"] else ""
    return f'''<section class="phase" id="{p["code"].lower()}">
  <div class="phase-head"><div class="phase-code">{p["code"]}</div><h3>{inline(p["name"])}</h3><div class="meta"><span class="chip {cls}">{html.escape(chip)}</span></div></div>
  {goal}{notes}
  <ol class="tasks">{tasks}</ol>
  {"<div class='checks'>" + checks + "</div>" if checks else ""}
  {res}
</section>'''


# ---------- page ----------

def build(out_path):
    md = PLAN.read_text(encoding="utf8")
    status = json.loads(STATUS.read_text(encoding="utf8"))
    secs = {t.split(".")[0]: (t, b) for t, b in split_sections(md, 2)}
    stages = parse_phases(secs["5"][1])
    all_phases = [p for s in stages for p in s["phases"]]

    # progress bar
    bar = ""
    for p in all_phases:
        state = status["phases"].get(p["code"], {}).get("state", "planned")
        c = "done" if state == "done" else ("next" if state in ("playtest", "review", "building", "next") else "")
        bar += f'<span class="{c}" title="{p["code"]} {html.escape(p["name"])}: {state}"></span>'

    log = "".join(f'<tr><td class="nowrap">{r["when"]}</td><td>{r["what"]}</td><td class="nowrap">{("<code>" + r["commit"] + "</code>") if r["commit"] else ""}</td></tr>'
                  for r in reversed(status["log"]))
    facts = "".join(f"<div><dt>{k}</dt><dd>{v}</dd></div>" for k, v in status["facts"])

    def todo_list(items, box):
        return '<ul class="tight todo">' + "".join(f'<li><span class="box">{box}</span>{x}</li>' for x in items) + "</ul>"
    t = status["todos"]
    todos = f'<h3>Next</h3>{todo_list(t["next"], "☐")}<h3>Still open</h3>{todo_list(t["open"], "☐")}<h3>Done</h3>{todo_list(t["done"], "☑")}'

    # request map with phase links
    req_rows = [r for k, v in blocks(secs["1"][1]) if k == "table" for r in v]
    req = '<div class="tbl-wrap"><table><thead><tr><th>#</th><th>Your request</th><th>Delivered in</th></tr></thead><tbody>'
    for r in req_rows[1:]:
        ph = re.sub(r"\b(M\d+[a-z]?)\b", lambda m: f'<a href="#{m.group(1).lower()}">{m.group(1)}</a>', inline(r[2]))
        req += f'<tr><td class="nowrap">{r[0]}</td><td>{inline(r[1])}</td><td>{ph}</td></tr>'
    req += "</tbody></table></div>"

    # decisions
    dec_rows = [r for k, v in blocks(secs["2"][1]) if k == "table" for r in v][1:]
    decs = ""
    for did, text in dec_rows:
        chip = '<span class="chip ok">confirmed</span>' if text.endswith("(C, standing rule)") or text.endswith("(C)") else '<span class="chip">proposed</span>'
        text = re.sub(r"\s*\((P|C)[^)]*\)\s*$", "", text)
        m = re.match(r"\*\*(.+?)\*\*\s*(.*)", text)
        title, rest = (m.group(1).rstrip("."), m.group(2)) if m else (did, text)
        decs += f'<article class="dec"><header><span class="id">{did}</span><h4>{inline(title)}</h4>{chip}</header><p>{inline(rest)}</p></article>'

    # questions
    q_rows = [r for k, v in blocks(secs["9"][1]) if k == "table" for r in v][1:]
    qs = '<div class="tbl-wrap"><table><thead><tr><th>#</th><th>Question</th><th>Options (suggested first)</th></tr></thead><tbody>'
    for qid, q, opts in q_rows:
        parts = [o.strip() for o in opts.split(" · ")]
        o_html = "".join(f'<div class="opt{" sug" if i == 0 else ""}">{inline(o)}{" <span class=chip>suggested</span>" if i == 0 else ""}</div>' for i, o in enumerate(parts))
        qs += f'<tr><td class="nowrap">{qid}</td><td>{inline(q)}</td><td>{o_html}</td></tr>'
    qs += "</tbody></table></div>"
    answers_html = ""
    if ANSWERS.exists():
        amd = ANSWERS.read_text(encoding="utf8")
        lead = amd.split("\n## ")[0]
        lead_p = [v for k, v in blocks("\n".join(lead.split("\n")[1:])) if k == "p"]
        answers_html = "".join(f'<p class="muted">{inline(x)}</p>' for x in lead_p)
        for t, b in split_sections(amd, 2):
            answers_html += f'<h3>{inline(t)}</h3><div class="prose">{generic_html(b)}</div>'
    personas_html = ""
    if PERSONAS.exists():
        pmd = PERSONAS.read_text(encoding="utf8")
        lead = pmd.split("\n## ")[0]
        personas_html = "".join(f'<p class="muted">{inline(v)}</p>' for k, v in blocks("\n".join(lead.split("\n")[1:])) if k == "p")
        for t, b in split_sections(pmd, 2):
            personas_html += f'<h3>{inline(t)}</h3><div class="prose">{generic_html(b)}</div>'
    q_note = "".join(f"<p class='muted' style='font-size:14px'>{inline(v)}</p>" for k, v in blocks(secs["9"][1]) if k == "p")

    # route
    route = ""
    for s in stages:
        route += f'<li class="stage"><div class="wp"></div><div class="body"><div class="eyebrow">{inline(s["title"])}</div></div></li>'
        for p in s["phases"]:
            st = status["phases"].get(p["code"], {}).get("state", "planned")
            cls, label = STATE_CHIP.get(st, ("", st))
            li = "done" if st == "done" else ""
            why = inline(p["goal"].split(". ")[0].rstrip(".") + ".") if p["goal"] else ""
            route += (f'<li class="{li}"><div class="wp">{p["code"]}</div><div class="body"><div class="title">'
                      f'<a href="#{p["code"].lower()}">{inline(p["name"])}</a><span class="chip {cls}">{label}</span></div>'
                      f'<p class="why">{why}</p></div></li>')

    phases_html = ""
    for s in stages:
        phases_html += f'<div class="stage-head"><div class="eyebrow">{inline(s["title"])}</div>{generic_html(s["pre"]) if s["pre"].strip() else ""}</div>'
        phases_html += "".join(phase_html(p, status) for p in s["phases"])

    proof = '<div class="tbl-wrap"><table><thead><tr><th>Date</th><th>Phase</th><th>Check</th><th>Result</th><th>Evidence</th></tr></thead><tbody>' + "".join(
        f'<tr><td class="nowrap">{r["date"]}</td><td class="nowrap">{r["phase"]}</td><td>{r["check"]}</td><td>{r["result"]}</td><td><code>{html.escape(r["evidence"])}</code></td></tr>'
        for r in reversed(status["proof"])) + "</tbody></table></div>"

    reviews = ""
    for rv in status["reviews"]:
        rows = "".join(f'<tr><td class="nowrap">{x["n"]}</td><td class="nowrap"><span class="chip {"crit" if x["sev"]=="Blocker" else ("moved" if x["sev"]=="Major" else "")}">{x["sev"]}</span></td><td>{x["where"]}</td><td>{x["finding"]}</td><td>{x["fix"]}</td></tr>' for x in rv["rows"])
        reviews += f'<h3>{html.escape(rv["title"])}</h3><p class="muted">{rv["summary"]}</p><div class="tbl-wrap"><table><thead><tr><th>#</th><th>Severity</th><th>Where</th><th>Finding</th><th>What was done</th></tr></thead><tbody>{rows}</tbody></table></div>'
    if not reviews:
        reviews = '<p class="muted">The plan review is running; its findings and fixes will appear here.</p>'

    gate_blocks = list(blocks(secs["4"][1]))
    gate_items = next(v for k, v in gate_blocks if k == "ol")
    gate = '<ol class="protocol">' + "".join(
        f'<li class="{"gate" if "Owner playtest" in it else ""}">{inline(it)}</li>' for it in gate_items) + "</ol>"
    cmds = next(v for k, v in gate_blocks if k == "code")

    arch = generic_html(secs["3"][1])
    risks = generic_html(secs["6"][1])
    ideas = generic_html(secs["7"][1]) + generic_html(secs["8"][1])

    nav_phases = "".join(f'<li><a href="#{p["code"].lower()}"><span class="code">{p["code"]}</span><span class="lab">{inline(p["name"].split(":")[0][:26])}</span></a></li>' for p in all_phases)
    style = STYLE.read_text(encoding="utf8")
    n = len(all_phases)
    done = sum(1 for p in all_phases if status["phases"].get(p["code"], {}).get("state") == "done")

    page = f'''<meta charset="utf-8">
<title>Rogue Pirates V5 Plan</title>
{style}
<style>
.progress{{grid-template-columns:repeat({n},minmax(0,1fr))}}
.route li.stage{{padding:18px 0 2px}}
.route li.stage .wp{{height:10px}}
.route li.stage .wp::after{{display:none}}
.stage-head{{padding-top:18px;border-top:1px solid var(--rule);display:flex;flex-direction:column;gap:8px}}
.stage-head .eyebrow{{font-size:13px;color:var(--brass)}}
.opt{{font-size:13.5px;padding:2px 0}}
.tid.d{{color:var(--k-g)}}
.opt.sug{{font-weight:600}}
.opt .chip{{margin-left:6px;vertical-align:1px}}
.stack{{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}}
@media (max-width:720px){{.stack{{grid-template-columns:minmax(0,1fr)}}}}
</style>
<div class="shell">
<nav class="nav" aria-label="Plan sections">
  <div class="brand">V5 Plan</div>
  <div class="navlists">
  <h4>Plan</h4>
  <ol>
    <li><a href="#progress"><span class="code">·</span><span>Progress</span></a></li>
    <li><a href="#todos"><span class="code">·</span><span>Your TODOs</span></a></li>
    <li><a href="#requests"><span class="code">·</span><span>Your 24 requests</span></a></li>
    <li><a href="#answers"><span class="code">·</span><span>Your answers</span></a></li>
    <li><a href="#questions"><span class="code">·</span><span>Open questions</span></a></li>
    <li><a href="#personalities"><span class="code">·</span><span>Player personalities</span></a></li>
    <li><a href="#decisions"><span class="code">·</span><span>Decisions</span></a></li>
    <li><a href="#route"><span class="code">·</span><span>Phase route</span></a></li>
    <li><a href="#stack"><span class="code">·</span><span>Stack findings</span></a></li>
    <li><a href="#architecture"><span class="code">·</span><span>Architecture</span></a></li>
    <li><a href="#protocol"><span class="code">·</span><span>Phase gate</span></a></li>
  </ol>
  <h4>Phases</h4>
  <ol>{nav_phases}</ol>
  <h4>Reference</h4>
  <ol>
    <li><a href="#proof"><span class="code">·</span><span>Proof and tests</span></a></li>
    <li><a href="#review"><span class="code">·</span><span>Review findings</span></a></li>
    <li><a href="#risks"><span class="code">·</span><span>Risks</span></a></li>
    <li><a href="#ideas"><span class="code">·</span><span>Decorations, brands, retention</span></a></li>
  </ol>
  </div>
</nav>

<main class="main">
<header class="masthead">
  <div class="eyebrow">Rogue Pirates · Pirate Game Unity · after V4</div>
  <h1>V5 Live Game Plan</h1>
  <span class="status{" ok" if status.get("statusOk") else ""}">{status["statusLine"]}</span>
  <p class="lede">Your 24 requests as {n} phases in six stages: a balanced voyage, a shop and mobile controls, accounts with custodial Solana devnet wallets and MagicBlock records, your island with defenses, raids and races, real-time Arena and Hunt with wagers, and a creator economy with sails, islands and brand bounties. Same way of working as V4: one module per phase, config as data, and a gate after every phase with tests, an independent review and your playtest.</p>
  <dl class="facts">{facts}</dl>
</header>

<section class="block" id="progress">
  <div class="sec-head"><div class="eyebrow">Updated {status["updated"]}</div><h2>Progress</h2>
  <p>{done} of {n} phases done. Newest first.</p></div>
  <div><div class="progress" role="img" aria-label="Phase progress: {done} of {n} done">{bar}</div>
  <div class="progress-lbl"><span>M0</span><span>{all_phases[-1]["code"]}</span></div></div>
  <div class="tbl-wrap"><table><thead><tr><th>When</th><th>What happened</th><th>Commit</th></tr></thead><tbody>{log}</tbody></table></div>
</section>

<section class="block" id="todos">
  <div class="sec-head"><div class="eyebrow">Updated {status["updated"]}</div><h2>Your TODOs</h2></div>
  <div class="todos">{todos}</div>
</section>

<section class="block" id="requests">
  <div class="sec-head"><div class="eyebrow">Coverage</div><h2>Your 24 requests</h2>
  <p>Where each request is built. The independent reviewer checks this map at every plan review.</p></div>
  {req}
</section>

<section class="block" id="answers">
  <div class="sec-head"><div class="eyebrow">30 Sep</div><h2>Your answers, explained</h2></div>
  {answers_html}
</section>

<section class="block" id="questions">
  <div class="sec-head"><div class="eyebrow">Needs your call</div><h2>Open questions</h2>
  <p>Answer on the GDD's "Questions for you" board (V5 section), then say "apply my answers". The suggested option is listed first.</p></div>
  {qs}
  {q_note}
</section>

<section class="block" id="personalities">
  <div class="sec-head"><div class="eyebrow">Balance simulator (M2)</div><h2>Player personalities</h2></div>
  {personas_html}
</section>

<section class="block" id="decisions">
  <div class="sec-head"><div class="eyebrow">Take effect when you approve the plan</div><h2>Decisions</h2></div>
  <div class="decisions">{decs}</div>
</section>

<section class="block" id="route">
  <div class="sec-head"><div class="eyebrow">Sequence</div><h2>Phase route</h2>
  <p>Each phase ends at the same gate: automated checks, the independent review, then your playtest. Stages can be reordered (question Q1).</p></div>
  <ol class="route">{route}</ol>
</section>

<section class="block" id="stack">
  <div class="sec-head"><div class="eyebrow">Research, 29 Sep</div><h2>Stack findings</h2>
  <p>What the web research found that shapes the plan. Details and sources: <code>plan/v5/research/</code>.</p></div>
  <div class="stack">
    <div class="check"><h4>Hosting: Railway only</h4><p>Unity's Multiplay hosting ended on 31 Mar 2026 and Hathora's in May 2026. Per your choice, everything runs on Railway: browser players reach our headless match server over secure WebSockets, which Railway supports.</p></div>
    <div class="check"><h4>SKR and ORE on devnet</h4><p>SKR has no devnet mint. ORE's devnet mint exists but only its program can mint. The game creates stand-in devnet mints with the same decimals (SKR 6, ORE 11) and a tester faucet.</p></div>
    <div class="check"><h4>MagicBlock</h4><p>VRF and Ephemeral Rollups are live on devnet (router devnet-router.magicblock.app). The BOLT framework is deprecated, so V5 uses plain Anchor with the rollup SDK.</p></div>
    <div class="check"><h4>Wallets and sign-in</h4><p>Per your choice, our own server holds each player's devnet wallet (encrypted in Postgres, master key in a Railway secret, your offline backup), with no key export for now. Sign-in is Google plus an email code. The game pays fees with an allow-list.</p></div>
    <div class="check"><h4>Royalties</h4><p>Metaplex Core's Royalties plugin at 5% is enforced only with an allow-list, which must include Tensor and Magic Eden so the NFTs still trade there.</p></div>
    <div class="check"><h4>Phones and browsers</h4><p>Chrome on 4–6 GB Android phones is 32-bit, so the game must stay near 256 MB of memory. Extension wallets don't inject in mobile browsers (players use the wallet's in-app browser). Web push on iOS works only for an installed web app.</p></div>
  </div>
</section>

<section class="block" id="architecture">
  <div class="sec-head"><div class="eyebrow">Target</div><h2>Architecture</h2>
  <p>The V4 run stays as it is; V5 adds meta services, a game server, the chain layer and netcode around it.</p></div>
  <div class="prose">{arch}</div>
</section>

<section class="block" id="protocol">
  <div class="sec-head"><div class="eyebrow">Repeated for every phase</div><h2>Phase gate</h2>
  <p>Step 9 is the independent reviewer (<code>.claude/agents/v5-reviewer.md</code>); step 11 is where work stops for your playtest.</p></div>
  {gate}
  <div class="copyrow"><h4>Test commands (repo root, Editor open)</h4><button class="btn" type="button" id="copyCmds">Copy</button></div>
  <pre><code id="cmds">{html.escape(cmds)}</code></pre>
</section>

<section class="block" id="phases">
  <div class="sec-head"><div class="eyebrow">Module task lists</div><h2>Phases in detail</h2>
  <p>Tasks, the automated check, what you playtest and when a phase is done. From M8 on, each phase's task list is detailed further when it starts, and the reviewer re-checks it.</p></div>
  <div class="kinds"><span><span class="tid i">I</span>implement</span><span><span class="tid g">G</span>generate or tune data</span><span><span class="tid n">N</span>integrate</span><span><span class="tid t">T</span>test</span><span><span class="tid c">C</span>setup</span><span><span class="tid d">D</span>document</span></div>
</section>
{phases_html}

<section class="block" id="proof">
  <div class="sec-head"><div class="eyebrow">Evidence</div><h2>Proof and tests</h2>
  <p>Every test run and check behind a phase result, newest first, with the file that holds it.</p></div>
  {proof}
</section>

<section class="block" id="review">
  <div class="sec-head"><div class="eyebrow">Independent reviewer</div><h2>Review findings</h2>
  <p>The plan review, then one table per phase: what the reviewer found and what was done about it.</p></div>
  {reviews}
</section>

<section class="block" id="risks">
  <div class="sec-head"><div class="eyebrow">Watch list</div><h2>Risks</h2></div>
  {risks}
</section>

<section class="block" id="ideas">
  <div class="sec-head"><div class="eyebrow">Ideas for your call</div><h2>Decorations, brands and retention</h2></div>
  <div class="prose">{ideas}</div>
</section>

<footer class="foot"><p>Source: <code>plan/v5/V5_PLAN.md</code> and <code>plan/v5/status.json</code> on branch <code>v5-live-game</code>, built by <code>plan/v5/tools/build_artifact.py</code>.</p></footer>
</main>
</div>
<script>
(function(){{
  var links = Array.prototype.slice.call(document.querySelectorAll('.nav a[href^="#"]'));
  var map = {{}};
  links.forEach(function(a){{ map[a.getAttribute('href').slice(1)] = a; }});
  var targets = Object.keys(map).map(function(id){{ return document.getElementById(id); }}).filter(Boolean);
  if ('IntersectionObserver' in window && targets.length){{
    var current = null;
    var io = new IntersectionObserver(function(entries){{
      entries.forEach(function(e){{
        if (e.isIntersecting){{
          if (current) current.classList.remove('on');
          current = map[e.target.id];
          if (current){{
            current.classList.add('on');
            if (window.matchMedia('(max-width:1000px)').matches && current.scrollIntoView){{
              current.scrollIntoView({{block:'nearest', inline:'center'}});
            }}
          }}
        }}
      }});
    }}, {{rootMargin:'-20% 0px -70% 0px'}});
    targets.forEach(function(t){{ io.observe(t); }});
  }}
  var btn = document.getElementById('copyCmds'), cmds = document.getElementById('cmds');
  if (btn && cmds){{
    btn.addEventListener('click', function(){{
      function selectFallback(){{
        var range = document.createRange(); range.selectNodeContents(cmds);
        var sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(range);
        btn.textContent = 'Selected';
      }}
      if (navigator.clipboard && navigator.clipboard.writeText){{
        navigator.clipboard.writeText(cmds.textContent).then(function(){{ btn.textContent = 'Copied'; }}, selectFallback);
      }} else {{ selectFallback(); }}
      setTimeout(function(){{ btn.textContent = 'Copy'; }}, 1800);
    }});
  }}
}})();
</script>
'''
    Path(out_path).write_text(page, encoding="utf8")
    print(f"wrote {out_path}: {len(page)} bytes, {n} phases, {done} done")


if __name__ == "__main__":
    build(sys.argv[1] if len(sys.argv) > 1 else "rogue-pirates-v5-plan.html")
