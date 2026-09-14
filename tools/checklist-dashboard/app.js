const SOURCES = [
  {
    id: "integration",
    name: "Integration Checklist",
    path: "../../Rogue_Pirates_Integration_Checklist.md",
    kind: "integration",
  },
  {
    id: "backlog",
    name: "Implementation Backlog",
    path: "../../Rogue_Pirates_Implementation_Backlog.md",
    kind: "checkbox",
  },
  {
    id: "traceability",
    name: "Traceability (plan/12)",
    path: "../../plan/12_TRACEABILITY_CHECKLIST.md",
    kind: "checkbox",
  },
];

const STATUS_COLORS = {
  PASS: "#3ecf8e",
  FIXED: "#6dd3a8",
  DONE: "#3ecf8e",
  IMPLEMENTED_UNVERIFIED: "#5eb3ff",
  IN_PROGRESS: "#5eb3ff",
  NEEDS_FIX: "#ff7b5c",
  NOT_STARTED: "#5c6b7d",
  TODO: "#5c6b7d",
  BLOCKED: "#c45cff",
  DEFERRED: "#7d8da0",
};

const DONE_STATUSES = new Set(["PASS", "FIXED", "DONE"]);
const PROGRESS_STATUSES = new Set(["IMPLEMENTED_UNVERIFIED", "IN_PROGRESS"]);

let activeDoc = SOURCES[0].id;
let documents = {};
let filterStatus = "";
let searchQuery = "";

const $ = (sel) => document.querySelector(sel);

function parseTableRow(line) {
  if (!line.startsWith("|")) return null;
  const cells = line.split("|").slice(1, -1).map((c) => c.trim());
  if (cells.length < 2 || cells.every((c) => /^-+$/.test(c))) return null;
  return cells;
}

function normalizeStatus(raw) {
  if (!raw) return "UNKNOWN";
  const cleaned = raw.replace(/`/g, "").trim().toUpperCase();
  if (cleaned.includes("NOT_STARTED")) return "NOT_STARTED";
  if (cleaned.includes("IMPLEMENTED_UNVERIFIED")) return "IMPLEMENTED_UNVERIFIED";
  if (cleaned.includes("IN_PROGRESS")) return "IN_PROGRESS";
  if (cleaned.includes("NEEDS_FIX")) return "NEEDS_FIX";
  if (cleaned.includes("DEFERRED")) return "DEFERRED";
  if (cleaned.includes("BLOCKED")) return "BLOCKED";
  if (cleaned === "PASS") return "PASS";
  if (cleaned === "FIXED") return "FIXED";
  if (cleaned === "DONE") return "DONE";
  if (cleaned === "TODO") return "TODO";
  return cleaned.split(/[\s/]/)[0] || "UNKNOWN";
}

function parseIntegrationMarkdown(text, source) {
  const lines = text.split(/\r?\n/);
  const items = [];
  let currentMilestone = "General";
  let currentSection = "General";
  let statusIdx = -1;

  for (const line of lines) {
    const h2 = line.match(/^## (.+)$/);
    if (h2) {
      currentMilestone = h2[1].trim();
      currentSection = currentMilestone;
      statusIdx = -1;
      continue;
    }

    const h3 = line.match(/^### (.+)$/);
    if (h3) {
      currentSection = h3[1].trim();
      statusIdx = -1;
      continue;
    }

    if (line.includes("|") && line.toLowerCase().includes("status")) {
      const headers = parseTableRow(line);
      if (headers) {
        statusIdx = headers.findIndex((h) => h.toLowerCase() === "status");
      }
      continue;
    }

    const row = parseTableRow(line);
    if (!row || statusIdx < 0) continue;

    const id = row[0];
    if (!/^(M\d|MX-|KI-)/.test(id)) continue;

    const status = normalizeStatus(row[statusIdx]);
    const title =
      row.find((c, i) => i > 0 && i !== statusIdx && c && !c.startsWith("`plan")) ||
      row[1] ||
      "";
    const evidence = row[statusIdx - 1] || "";
    const acceptance = row[statusIdx + 1] || "";

    items.push({
      id,
      status,
      milestone: currentMilestone,
      section: currentSection,
      title,
      evidence,
      acceptance,
      source,
      searchText: [id, status, title, evidence, acceptance, currentMilestone, currentSection]
        .join(" ")
        .toLowerCase(),
    });
  }

  return items;
}

function parseCheckboxMarkdown(text, source) {
  const lines = text.split(/\r?\n/);
  const items = [];
  let currentMilestone = "General";
  let gate = "";

  for (const line of lines) {
    const h2 = line.match(/^## (.+)$/);
    if (h2) {
      currentMilestone = h2[1].trim();
      gate = "";
      continue;
    }

    const h3 = line.match(/^### (.+)$/);
    if (h3) {
      gate = h3[1].trim();
      continue;
    }

    const item = line.match(/^- \[( |x|X)\] (.+)$/);
    if (!item) continue;

    const done = item[1].toLowerCase() === "x";
    const textValue = item[2].trim();
    const idMatch = textValue.match(/^(M\d+-\d+|G\d+|RP-[A-Z]+-\d+)/);
    const id = idMatch ? idMatch[1] : `${source.id}-${items.length + 1}`;

    items.push({
      id,
      status: done ? "DONE" : "TODO",
      milestone: currentMilestone,
      section: gate || currentMilestone,
      title: textValue,
      evidence: "",
      acceptance: "",
      source,
      searchText: [id, textValue, currentMilestone, gate].join(" ").toLowerCase(),
    });
  }

  return items;
}

function parseMarkdown(text, source) {
  if (source.kind === "integration") return parseIntegrationMarkdown(text, source.id);
  return parseCheckboxMarkdown(text, source.id);
}

function computeStats(items) {
  const byStatus = {};
  const byMilestone = {};

  for (const item of items) {
    byStatus[item.status] = (byStatus[item.status] || 0) + 1;

    const key = item.milestone.replace(/^##\s*/, "").split("—")[0].trim();
    if (!byMilestone[key]) {
      byMilestone[key] = { total: 0, done: 0, progress: 0 };
    }
    byMilestone[key].total += 1;
    if (DONE_STATUSES.has(item.status)) byMilestone[key].done += 1;
    if (PROGRESS_STATUSES.has(item.status)) byMilestone[key].progress += 1;
  }

  const total = items.length;
  const done = items.filter((i) => DONE_STATUSES.has(i.status)).length;
  const progress = items.filter((i) => PROGRESS_STATUSES.has(i.status)).length;
  const needsFix = items.filter((i) => i.status === "NEEDS_FIX").length;
  const notStarted = items.filter((i) => i.status === "NOT_STARTED" || i.status === "TODO").length;

  return { total, done, progress, needsFix, notStarted, byStatus, byMilestone };
}

async function loadDocuments() {
  const results = {};
  const errors = [];

  for (const source of SOURCES) {
    try {
      const res = await fetch(source.path);
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
      const text = await res.text();
      const items = parseMarkdown(text, source);
      results[source.id] = {
        source,
        items,
        stats: computeStats(items),
        loadedAt: new Date(),
      };
    } catch (err) {
      errors.push(`${source.name}: ${err.message}`);
      results[source.id] = {
        source,
        items: [],
        stats: computeStats([]),
        loadedAt: new Date(),
        error: String(err),
      };
    }
  }

  documents = results;
  return errors;
}

function groupItems(items) {
  const groups = new Map();
  for (const item of items) {
    const key = item.section || item.milestone;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(item);
  }
  return groups;
}

function matchesFilters(item) {
  if (filterStatus && item.status !== filterStatus) return false;
  if (searchQuery && !item.searchText.includes(searchQuery)) return false;
  return true;
}

function statusClass(status) {
  return `status-${status.replace(/[^A-Z0-9_]/g, "_")}`;
}

function renderLegend(allStatuses) {
  const legend = $("#legend");
  legend.innerHTML = "";
  for (const status of allStatuses.sort()) {
    const li = document.createElement("li");
    const dot = document.createElement("span");
    dot.className = "dot";
    dot.style.background = STATUS_COLORS[status] || "#888";
    li.appendChild(dot);
    li.append(`${status} (${documents[activeDoc]?.stats.byStatus[status] || 0})`);
    legend.appendChild(li);
  }
}

function renderStatusFilter(stats) {
  const select = $("#status-filter");
  const current = select.value;
  select.innerHTML = '<option value="">All statuses</option>';
  for (const status of Object.keys(stats.byStatus).sort()) {
    const opt = document.createElement("option");
    opt.value = status;
    opt.textContent = `${status} (${stats.byStatus[status]})`;
    select.appendChild(opt);
  }
  select.value = current;
}

function renderTabs() {
  const tabs = $("#doc-tabs");
  tabs.innerHTML = "";
  for (const source of SOURCES) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = `tab${source.id === activeDoc ? " active" : ""}`;
    const stats = documents[source.id]?.stats;
    btn.textContent = stats
      ? `${source.name} (${stats.done}/${stats.total})`
      : source.name;
    btn.addEventListener("click", () => {
      activeDoc = source.id;
      render();
    });
    tabs.appendChild(btn);
  }
}

function renderSummary(stats) {
  const el = $("#summary-stats");
  const pct = stats.total ? Math.round((stats.done / stats.total) * 100) : 0;
  el.innerHTML = `
    <div class="stat"><span class="label">Total items</span><span class="value">${stats.total}</span></div>
    <div class="stat"><span class="label">Complete</span><span class="value">${stats.done}</span></div>
    <div class="stat"><span class="label">In progress</span><span class="value">${stats.progress}</span></div>
    <div class="stat"><span class="label">Completion</span><span class="value">${pct}%</span></div>
    <div class="stat"><span class="label">Needs fix</span><span class="value">${stats.needsFix}</span></div>
    <div class="stat"><span class="label">Not started</span><span class="value">${stats.notStarted}</span></div>
  `;
}

function renderMilestoneBars(stats) {
  const el = $("#milestone-bars");
  el.innerHTML = "";
  const entries = Object.entries(stats.byMilestone).filter(([name]) => !name.startsWith("Known"));
  for (const [name, data] of entries) {
    const pct = data.total ? Math.round(((data.done + data.progress * 0.5) / data.total) * 100) : 0;
    const row = document.createElement("div");
    row.className = "milestone-row";
    row.innerHTML = `
      <div class="label-row"><span>${name}</span><span>${data.done}/${data.total}</span></div>
      <div class="bar-track"><div class="bar-fill" style="width:${pct}%"></div></div>
    `;
    el.appendChild(row);
  }
}

function renderCards(stats) {
  const el = $("#dashboard-cards");
  const visible = documents[activeDoc].items.filter(matchesFilters).length;
  el.innerHTML = `
    <div class="card"><h3>Document</h3><div class="big">${documents[activeDoc].source.name}</div></div>
    <div class="card"><h3>Visible rows</h3><div class="big">${visible}</div></div>
    <div class="card"><h3>Needs attention</h3><div class="big">${stats.needsFix + stats.notStarted}</div></div>
    <div class="card"><h3>Verified pass</h3><div class="big">${stats.byStatus.PASS || 0}</div></div>
  `;
}

function renderSections(items) {
  const container = $("#sections");
  container.innerHTML = "";
  const groups = groupItems(items);

  for (const [sectionName, sectionItems] of groups) {
    const visibleItems = sectionItems.filter(matchesFilters);
    const section = document.createElement("section");
    section.className = "section";
    section.innerHTML = `
      <div class="section-header">
        <h2>${sectionName}</h2>
        <span class="section-count">${visibleItems.length} / ${sectionItems.length} shown</span>
      </div>
      <div class="section-body"></div>
    `;
    const body = section.querySelector(".section-body");

    for (const item of sectionItems) {
      const row = document.createElement("article");
      row.className = `item-row${matchesFilters(item) ? "" : " hidden"}`;
      row.innerHTML = `
        <div class="item-head">
          <span class="item-id">${item.id}</span>
          <span class="item-status ${statusClass(item.status)}">${item.status}</span>
        </div>
        <p class="item-title">${escapeHtml(item.title)}</p>
        <div class="item-meta">
          ${item.evidence ? `<div><strong>Evidence:</strong> ${escapeHtml(item.evidence)}</div>` : ""}
          ${item.acceptance ? `<div><strong>Acceptance:</strong> ${escapeHtml(item.acceptance)}</div>` : ""}
        </div>
      `;
      body.appendChild(row);
    }

    container.appendChild(section);
  }
}

function escapeHtml(str) {
  return str
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function render() {
  const doc = documents[activeDoc];
  if (!doc) return;

  const { stats, loadedAt, error } = doc;
  renderTabs();
  renderSummary(stats);
  renderMilestoneBars(stats);
  renderCards(stats);
  renderStatusFilter(stats);
  renderLegend(Object.keys(stats.byStatus));

  const meta = $("#meta-line");
  meta.textContent = `Last loaded: ${loadedAt.toLocaleString()} · ${doc.source.path}`;

  const sections = $("#sections");
  if (error) {
    sections.innerHTML = `<div class="error-banner"><strong>Could not load markdown.</strong> ${escapeHtml(error)}<br><br>Run <code>tools/checklist-dashboard/serve.ps1</code> from the repo root (browsers block <code>file://</code> fetch).</div>`;
    return;
  }

  renderSections(doc.items);
}

async function init() {
  $("#search").addEventListener("input", (e) => {
    searchQuery = e.target.value.trim().toLowerCase();
    render();
  });

  $("#status-filter").addEventListener("change", (e) => {
    filterStatus = e.target.value;
    render();
  });

  $("#refresh").addEventListener("click", async () => {
    $("#refresh").textContent = "Loading…";
    await loadDocuments();
    $("#refresh").textContent = "Refresh";
    render();
  });

  await loadDocuments();
  render();
}

init();
