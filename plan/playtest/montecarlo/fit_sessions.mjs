// V5 M9a (C9a.1, 8 Oct 2026; first written for M8z I8z.7): fits the model's free constants (MODEL in sim.mjs) on the
// owner's recorded voyages (plan/playtest/sessions), so that for every voyage the model's p10-p90 band contains the
// voyage's level, kills, hull and damage taken each minute, and its survival time.
//
// Each voyage is replayed on the config it was played on (session_data.mjs: one data snapshot per voyage, plus the XP
// payout of its build), not on today's: the five voyages span four builds (island hits 10 or 5 hull, XP pace 0.5 with
// the old per-piece payout, 0.8 and 0.4 exact, weapon damage before and after D20), and fitting them all to today's
// config would make the constants absorb those differences. The MODEL constants describe the player and the physics the
// model leaves out (aim, reach, dodging, barrels missed), which do not change with the config, so one set is fitted on
// all five. The voyage on today's balance (S5) weighs most, then S4 (the same rules but XP 0.8).
//
//   node fit_sessions.mjs                 (search, then print and save calibration-sessions.json)
//   node fit_sessions.mjs --report        (no search: the current MODEL against each voyage)
//   node fit_sessions.mjs --report --set key=value ...
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { simulateRun, MODEL } from './sim.mjs';
import { SESSIONS, loadSessionData } from './session_data.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const sorted = a => a.filter(x => x != null && !Number.isNaN(x)).sort((x, y) => x - y);
export const q = (a, p) => { const s = sorted(a); return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : null; };
const med = a => q(a, 0.5);
export const REPORT_MINUTES = [1, 2, 4, 7, 10, 14];
/** Fit weight per voyage: today's balance first, then the D20 rules, then the older builds (S1 is short). */
export const WEIGHTS = { S1: 0.5, S2: 0.8, S3: 0.8, S4: 1.5, S5: 2.5 };

export function loadSessions() {
  return SESSIONS.map(meta => ({ meta, file: meta.file, s: JSON.parse(fs.readFileSync(path.join(here, '..', 'sessions', meta.file), 'utf8')), ...loadSessionData(meta) }));
}

/** The game's numbers for one voyage: per full minute level, cumulative kills, hull share, cumulative damage taken (every source); the end. */
export function gameSide(s) {
  const H = s.header, end = H.endTime, samples = s.samples;
  const at = t => { let b = null; for (const x of samples) if (x.t <= t + 0.01) b = x; return b; };
  const by = (s.damageTaken && s.damageTaken.bySource) || {};
  const minutes = [];
  for (let m = 1; m <= Math.floor(end / 60); m++) { const x = at(m * 60); minutes.push({ m, level: x.level, kills: x.kills, hull: x.hull / x.maxHull, dmg: x.damageTaken, dps: x.dps, elites: x.elitesAlive, enemies: x.enemies }); }
  const last = samples[samples.length - 1];
  const fights = {};
  for (const f of s.focus || []) { const k = f.kind === 'elite' && f.adaptive ? 'adaptive elite' : f.kind; if (f.kill >= 0) (fights[k] ??= []).push(f.kill - f.spawn); }
  const weaponOrder = (s.events || []).filter(e => e.type === 'pick' && e.kind === 'NewWeapon').map(e => e.id);
  const pickPlan = (s.events || []).filter(e => e.type === 'pick').map(e => ({ kind: e.kind, id: e.id }));
  const total = Object.values(by).reduce((a, x) => a + x.damage, 0);
  return { end, weaponOrder, pickPlan, sunk: H.endReason === 'sunk', bossAt: H.bossSummoned >= 0 ? Math.round(H.bossSummoned) : null, captain: H.captain, weapon: H.startingWeapon, minutes,
    final: { level: last.level, kills: last.kills, hull: last.hull / last.maxHull, dmg: last.damageTaken },
    fights: Object.fromEntries(Object.entries(fights).map(([k, v]) => [k, med(v)])), by, total,
    island: by.island ? by.island.damage : 0, heals: s.heals ? s.heals.total : 0 };
}

/** The model's runs for one voyage (on its own config), to maxMinutes (20 by default, so the survival time is seen). */
export function runSession(data, g, model, seeds, maxMinutes = 20) {
  const runs = [];
  for (let i = 0; i < seeds; i++) runs.push(simulateRun(data, { captain: data.captains[g.captain] ? g.captain : Object.keys(data.captains)[0], startWeapon: data.weapons[g.weapon] ? g.weapon : undefined,
    seed: (i + 1) * 7919 + 17, bossAt: g.bossAt, weaponOrder: g.weaponOrder, pickPlan: g.pickPlan, model: { ...model, maxMinutes } }));
  return runs;
}

const band = a => { const s = sorted(a); return s.length ? [q(s, 0.1), q(s, 0.5), q(s, 0.9)] : null; };
/** A model run's state at a time (seconds; at most its end): level, cumulative kills and damage, hull share. */
const stateAt = (r, t) => {
  const m = Math.floor(t / 60), row = r.minutes.find(x => x.m === m);
  const cum = k => r.minutes.filter(x => x.m <= m).reduce((s, x) => s + x[k], 0);
  return row ? { level: row.level, kills: cum('kills'), dmg: cum('dmg'), hull: row.hull, elites: row.elites, enemies: row.alive } : null;
};

/**
 * The model's numbers for one voyage: per minute the p10 / median / p90 over the runs still afloat (level, kills, hull,
 * damage), the share afloat; the end-time distribution and the share sunk by the voyage's end. `kindOf(id)` names a fight.
 */
export function modelSide(runs, g, kindOf) {
  const minutes = g.minutes.map(({ m }) => {
    const rows = runs.filter(r => r.minutes.some(x => x.m === m && x.hull > 0)).map(r => stateAt(r, m * 60));
    return { m, afloat: rows.length / runs.length, level: band(rows.map(x => x.level)), kills: band(rows.map(x => x.kills)), hull: band(rows.map(x => x.hull)), dmg: band(rows.map(x => x.dmg)), elites: band(rows.map(x => x.elites)), enemies: band(rows.map(x => x.enemies)) };
  });
  const fights = {};
  for (const r of runs) for (const [id, start, secs] of r.focus) if (start <= g.end) (fights[kindOf(id, start)] ??= []).push(secs);
  const ends = runs.map(r => r.died ? r.end * 60 : Infinity);
  const sunkBy = runs.filter(r => r.died && r.end * 60 <= g.end).length / runs.length;
  // Where the voyage's end sits in the model's end-time distribution (0..1): the share of runs that sank before it.
  const endRank = ends.filter(t => t < g.end).length / runs.length;
  const by = {}; for (const r of runs) for (const [k, v] of Object.entries(r.damageBy)) by[k] = (by[k] ?? 0) + v / runs.length;
  return { minutes, fights: Object.fromEntries(Object.entries(fights).map(([k, v]) => [k, med(v)])), sunkBy, endRank,
    end: [q(ends, 0.1), q(ends, 0.5), q(ends, 0.9)], runs: runs.length, by,
    island: med(runs.map(r => r.islandDamage ?? 0)), heals: med(runs.map(r => r.healed ?? 0)), elites: band(runs.map(r => (r.minutes.find(x => x.m === 10) || {}).elites)) };
}

export const kindOfNew = data => id => {
  if (id.startsWith('adaptive:')) return 'adaptive elite';
  if (data.enemies[id] && data.enemies[id].boss) return 'boss';
  const p = data.timeline.focus.find(x => x.id === id);
  return p && data.enemies[p.enemy] && data.enemies[p.enemy].miniboss ? 'miniboss' : 'elite';
};

/**
 * One loss over the voyages: a proper score, so a wide band is not free. Per minute and metric (level, kills, damage
 * taken, hull; elites and enemies alive at lower weight): log(spread) + z^2 / 2, where z is the voyage's gap to the model's
 * median in model spreads (the p10-p90 width / 2.56, floored), plus 1 when the voyage is outside p10-p90. Survival:
 * where the voyage's end sits among the model's end times (its rank; a sunk voyage should sit near the middle and inside
 * p10-p90, a voyage that ended afloat not in the sunk tail). Weighted by voyage (WEIGHTS), spread over its minutes.
 */
const FLOOR = { level: 0.5, kills: 4, dmg: 2, hull: 0.03, elites: 0.5, enemies: 1 };
const METRIC_WEIGHT = { level: 1, kills: 1, dmg: 1, hull: 0.5, elites: 0.4, enemies: 0.3 };
export function score(gv, b, key) {
  if (!b) return 8;
  const spread = Math.max((b[2] - b[0]) / 2.56, Math.abs(b[1]) * 0.03, FLOOR[key]), ref = Math.max(Math.abs(gv) * 0.08, FLOOR[key] * 2);
  const z = (gv - b[1]) / spread, out = gv < b[0] || gv > b[2] ? 1 : 0;
  return Math.min(20, Math.log(spread / ref) + 0.5 * z * z + out);
}
export function loss(pairs) {
  let sum = 0, n = 0;
  const add = (v, w) => { if (v != null && Number.isFinite(v)) { sum += w * v; n += w; } };
  for (const { g, m, meta } of pairs) {
    const w = WEIGHTS[meta.id] ?? 1, wm = w / Math.max(1, g.minutes.length);
    for (let i = 0; i < g.minutes.length; i++) {
      const a = g.minutes[i], b = m.minutes[i];
      for (const k of Object.keys(METRIC_WEIGHT)) add(b.afloat < 0.03 || !b.level ? 20 : score(a[k], b[k], k), wm * METRIC_WEIGHT[k]);
    }
    // Survival, counted like four minutes' metrics.
    const r = m.endRank, tail = g.sunk ? Math.max(0, 0.1 - r, r - 0.9) : Math.max(0, r - 0.5);
    add(Math.min(20, (g.sunk ? ((r - 0.5) / 0.3) ** 2 : 0) + (tail / 0.1) ** 2 + (tail > 0 ? 1 : 0)), w * 4 * 4.4 / Math.max(1, g.minutes.length));
  }
  return sum / Math.max(1e-9, n);
}

export function evaluate(sessions, gs, model, seeds) {
  const pairs = sessions.map((x, i) => {
    const runs = runSession(x.data, gs[i], { ...model, ...x.model }, seeds);
    return { meta: x.meta, g: gs[i], m: modelSide(runs, gs[i], kindOfNew(x.data)) };
  });
  return { loss: loss(pairs), pairs };
}

const clock = s => s == null ? '-' : !Number.isFinite(s) ? '>20:00' : `${Math.floor(s / 60)}:${String(Math.round(s % 60)).padStart(2, '0')}`;
const fmtBand = (b, d = 0, k = 1) => b ? `${(b[1] * k).toFixed(d)} (${(b[0] * k).toFixed(d)}-${(b[2] * k).toFixed(d)})` : '-';
const inside = (v, b) => b && v >= b[0] && v <= b[2];
/** The per-voyage table: minutes 1, 2, 4, 7, 10, 14 and the end; a mark where the voyage is outside the model's p10-p90. */
export function printPair({ g, m, meta }) {
  console.log(`\n${meta.id} ${meta.file} (config ${meta.configHash}, ${meta.note}): ${g.sunk ? 'sunk' : 'ended'} at ${clock(g.end)}`);
  console.log(`  survival: model end p10 ${clock(m.end[0])}, p50 ${clock(m.end[1])}, p90 ${clock(m.end[2])}; sunk by ${clock(g.end)} in ${(m.sunkBy * 100).toFixed(0)}% of runs (the voyage sits at the ${(m.endRank * 100).toFixed(0)}th percentile)`);
  console.log('  min   level game | model             kills game | model           hull% game | model     damage game | model');
  const rows = g.minutes.map((a, i) => ({ a, b: m.minutes[i] })).filter(({ a }) => REPORT_MINUTES.includes(a.m) || a.m === g.minutes.length);
  for (const { a, b } of rows) {
    const mark = (v, x) => inside(v, x) ? ' ' : '*';
    console.log(`  ${String(a.m).padStart(3)}   ${String(a.level).padStart(3)}${mark(a.level, b.level)}| ${fmtBand(b.level).padEnd(14)}  ${String(a.kills).padStart(5)}${mark(a.kills, b.kills)}| ${fmtBand(b.kills).padEnd(18)} ${String(Math.round(a.hull * 100)).padStart(3)}${mark(a.hull, b.hull)}| ${fmtBand(b.hull, 0, 100).padEnd(12)}  ${String(Math.round(a.dmg)).padStart(4)}${mark(a.dmg, b.dmg)}| ${fmtBand(b.dmg)}  (afloat ${(b.afloat * 100).toFixed(0)}%)`);
  }
}

// The searched constants and their grids. Measured ones (island and edge-hazard rates) are set from the voyages in MODEL.
export const KNOBS = {
  accuracy: [0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5, 0.6, 0.7],
  engagement: [0.75, 0.8, 0.85, 0.9, 0.95, 1],
  arrivalSeconds: [0.5, 1, 1.5, 2, 3, 4],
  lootShare: [0.5, 0.6, 0.7, 0.8, 0.85, 0.9, 0.95, 1],
  barrelsPerMinute: [60, 80, 100, 120, 140, 170, 1000],
  arenaShareOfDps: [0.15, 0.2, 0.3, 0.45, 0.6],
  focusShareOfDps: [0.05, 0.1, 0.15, 0.25],
  eliteShareOfDps: [0.01, 0.02, 0.03, 0.04, 0.06, 0.1],
  contactRate: [0.02, 0.03, 0.045, 0.06, 0.08, 0.1, 0.12, 0.16],
  shotHitChance: [0.01, 0.02, 0.04, 0.06, 0.08, 0.12, 0.16, 0.2],
  focusHitsPerSecond: [0.05, 0.1, 0.15, 0.2, 0.3],
  playSpread: [0, 0.05, 0.1, 0.15],
  exposureSpread: [0, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8],
  // Measured lower bound 0.6 (the barrels picked up at full hull leave no heal event), so searched from there.
  healShare: [0.6, 0.7, 0.8, 0.9, 1],
};

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2), report = args.includes('--report'), set = {};
  for (let i = 0; i < args.length; i++) if (args[i] === '--set') { const [k, v] = args[++i].split('='); set[k] = +v; }
  const sessions = loadSessions(), gs = sessions.map(x => gameSide(x.s));
  let best = { ...Object.fromEntries(Object.keys(KNOBS).map(k => [k, MODEL[k]])), ...set };
  const seedsSearch = 80;
  let bestLoss = evaluate(sessions, gs, best, seedsSearch).loss;
  console.log(`start loss ${bestLoss.toFixed(3)}`);
  if (!report) {
    for (let round = 0; round < 4; round++) {
      let moved = false;
      for (const [k, values] of Object.entries(KNOBS)) {
        for (const v of values) {
          if (v === best[k]) continue;
          const l = evaluate(sessions, gs, { ...best, [k]: v }, seedsSearch).loss;
          if (l < bestLoss - 1e-4) { bestLoss = l; best = { ...best, [k]: v }; moved = true; }
        }
        console.log(`round ${round + 1} ${k.padEnd(22)} ${String(best[k]).padEnd(6)} loss ${bestLoss.toFixed(3)}`);
      }
      if (!moved) break;
    }
  }
  const final = evaluate(sessions, gs, best, 200);
  console.log(`\nfinal loss (200 seeds) ${final.loss.toFixed(3)}; constants:`, JSON.stringify(best));
  for (const p of final.pairs) printPair(p);
  if (!report) fs.writeFileSync(path.join(here, 'calibration-sessions.json'), JSON.stringify({ fitted: new Date().toISOString(), loss: final.loss, weights: WEIGHTS,
    sessions: SESSIONS.map(s => ({ id: s.id, file: s.file, configHash: s.configHash, playedOn: s.commit, today: s.today })), model: best }, null, 1));
}
