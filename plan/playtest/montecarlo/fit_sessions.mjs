// V5 M8z (I8z.7, owner 7 Oct 2026): fits the model's free constants (MODEL in sim.mjs) on the owner's recorded voyages
// (plan/playtest/sessions), so the per-minute level, kills, hull and damage taken and the fight lengths land in the
// sessions' range. The data is the game's config (data.json, the Unity export); only the MODEL constants move.
//
//   node fit_sessions.mjs                 (search, then print and save calibration-sessions.json)
//   node fit_sessions.mjs --report        (no search: the current MODEL against each session)
//
// The sessions were played on three slightly different configs (their config hashes differ; the first one had island
// hits at 10 hull): the fit is to the game as it is now, with the sessions as the closest measurements of real play.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { simulateRun, MODEL } from './sim.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
export const SESSIONS = ['session-20261006-152859-rook.json', 'session-20261007-122453-rook.json', 'session-20261007-141608-rook.json'];
const med = a => { const s = a.filter(x => x != null && !Number.isNaN(x)).sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : null; };
const q = (a, p) => { const s = a.filter(x => x != null && !Number.isNaN(x)).sort((x, y) => x - y); return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : null; };

export function loadSessions() { return SESSIONS.map(f => ({ file: f, s: JSON.parse(fs.readFileSync(path.join(here, '..', 'sessions', f), 'utf8')) })); }

/** The game's numbers for one session: per full minute level, cumulative kills, hull share, cumulative damage (enemies and islands), DPS; fights. */
export function gameSide(s) {
  const H = s.header, end = H.endTime, samples = s.samples;
  const at = t => { let b = null; for (const x of samples) if (x.t <= t + 0.01) b = x; return b; };
  const by = (s.damageTaken && s.damageTaken.bySource) || {}, total = Object.values(by).reduce((a, x) => a + x.damage, 0);
  const other = ['storm', 'stage_hit', 'hazard', 'other'].reduce((a, k) => a + (by[k] ? by[k].damage : 0), 0);
  const modelled = total > 0 ? (total - other) / total : 1;
  const minutes = [];
  for (let m = 1; m <= Math.floor(end / 60); m++) { const x = at(m * 60); minutes.push({ m, level: x.level, kills: x.kills, hull: x.hull / x.maxHull, dmg: x.damageTaken * modelled, dps: x.dps }); }
  const fights = {};
  for (const f of s.focus || []) { const k = f.kind === 'elite' && f.adaptive ? 'adaptive elite' : f.kind; if (f.kill >= 0) (fights[k] ??= []).push(f.kill - f.spawn); }
  const weaponOrder = (s.events || []).filter(e => e.type === 'pick' && e.kind === 'NewWeapon').map(e => e.id);
  const pickPlan = (s.events || []).filter(e => e.type === 'pick').map(e => ({ kind: e.kind, id: e.id }));
  return { end, weaponOrder, pickPlan, sunk: H.endReason === 'sunk', bossAt: H.bossSummoned >= 0 ? Math.round(H.bossSummoned) : null, captain: H.captain, weapon: H.startingWeapon, minutes,
    fights: Object.fromEntries(Object.entries(fights).map(([k, v]) => [k, med(v)])), island: by.island ? by.island.damage : 0, heals: s.heals ? s.heals.total : 0 };
}

/**
 * The model's numbers for one session (seeds runs; the boss at the session's Beacon time): per minute the median over the
 * runs still afloat, the share afloat, and fight medians. `kindOf(id)` names a fight (miniboss, elite, adaptive elite, boss).
 * `levelOffset` turns the model's level into the game's (0 now; 1 for the pre-M8z model, which counted from 1).
 */
export function modelSide(sim, data, g, model, seeds, kindOf, levelOffset = 0) {
  const runs = [];
  for (let i = 0; i < seeds; i++) runs.push(sim(data, { captain: data.captains[g.captain] ? g.captain : Object.keys(data.captains)[0], startWeapon: data.weapons[g.weapon] ? g.weapon : undefined, seed: (i + 1) * 7919 + 17, bossAt: g.bossAt, weaponOrder: g.weaponOrder, pickPlan: g.pickPlan, model: { ...model, maxMinutes: Math.ceil(g.end / 60) + 1 } }));
  const minutes = g.minutes.map(({ m }) => {
    const rows = runs.filter(r => r.minutes.some(x => x.m === m && x.hull > 0));
    const cum = (r, k) => r.minutes.filter(x => x.m <= m).reduce((s, x) => s + x[k], 0);
    const row = r => r.minutes.find(x => x.m === m);
    return { m, afloat: rows.length / runs.length, level: med(rows.map(r => row(r).level - levelOffset)), kills: med(rows.map(r => cum(r, 'kills'))), hull: med(rows.map(r => row(r).hull)),
      dmg: med(rows.map(r => cum(r, 'dmg'))), dps: med(rows.map(r => row(r).gameDps)), levelP: [q(rows.map(r => row(r).level - levelOffset), 0.1), q(rows.map(r => row(r).level - levelOffset), 0.9)],
      killsP: [q(rows.map(r => cum(r, 'kills')), 0.1), q(rows.map(r => cum(r, 'kills')), 0.9)] };
  });
  const fights = {};
  for (const r of runs) for (const [id, start, secs] of r.focus) if (start <= g.end) (fights[kindOf(id, start)] ??= []).push(secs);
  const sunkBy = runs.filter(r => r.died && r.end * 60 <= g.end + 60).length / runs.length;
  return { minutes, fights: Object.fromEntries(Object.entries(fights).map(([k, v]) => [k, med(v)])), sunkBy, endMedian: med(runs.map(r => r.end * 60)),
    island: med(runs.map(r => r.islandDamage ?? 0)), heals: med(runs.map(r => r.healed ?? 0)) };
}

export const kindOfNew = data => id => {
  if (id.startsWith('adaptive:')) return 'adaptive elite';
  if (data.enemies[id] && data.enemies[id].boss) return 'boss';
  const p = data.timeline.focus.find(x => x.id === id);
  return p && data.enemies[p.enemy] && data.enemies[p.enemy].miniboss ? 'miniboss' : 'elite';
};

/** One loss over the sessions: relative errors per minute (level, kills, damage, hull), fight lengths, and survival. */
export function loss(pairs) {
  let sum = 0, n = 0;
  const add = (v, w = 1) => { if (v != null && Number.isFinite(v)) { sum += w * v * v; n += w; } };
  for (const { g, m } of pairs) {
    const w = g.minutes.length >= 8 ? 1 : 0.6;
    for (let i = 0; i < g.minutes.length; i++) {
      const a = g.minutes[i], b = m.minutes[i];
      if (b.afloat < 0.05 || b.level == null) { add(1.5, w); continue; }
      add((b.level - a.level) / Math.max(3, a.level * 0.12), w);
      add((b.kills - a.kills) / Math.max(30, a.kills * 0.15), w);
      add((b.dmg - a.dmg) / Math.max(15, a.dmg * 0.2), w);
      add((b.hull - a.hull) / 0.25, w * 0.5);
      if (a.dps > 0 && b.dps > 0) add(Math.log(b.dps / a.dps) / 0.5, w * 0.5); // the theoretical DPS (data-driven; follows the level)
    }
    // The player was afloat until the end: most runs should be too, a minute before it.
    const last = m.minutes[Math.max(0, m.minutes.length - 2)];
    if (last) add(Math.max(0, 0.6 - last.afloat) / 0.2, w * 2);
    if (g.sunk) add(Math.max(0, 0.3 - m.sunkBy) / 0.2, w);
    for (const k of ['miniboss', 'elite', 'adaptive elite']) if (g.fights[k] != null && m.fights[k] != null) add(Math.log(m.fights[k] / g.fights[k]) / 0.35, w);
  }
  return Math.sqrt(sum / Math.max(1, n));
}

const KNOBS = {
  accuracy: [0.25, 0.3, 0.35, 0.4, 0.45, 0.5, 0.6, 0.7],
  engagement: [0.7, 0.8, 0.9, 1],
  arrivalSeconds: [0.5, 1, 1.5, 2, 3, 4],
  lootShare: [0.7, 0.8, 0.85, 0.9, 0.95, 1],
  vfxDensity: [0.45, 0.6, 0.75],
  barrelsPerMinute: [80, 100, 120, 140, 170, 1000],
  arenaShareOfDps: [0.15, 0.2, 0.3, 0.45, 0.6, 0.75],
  focusShareOfDps: [0.05, 0.1, 0.15, 0.25, 0.35],
  eliteShareOfDps: [0.01, 0.02, 0.03, 0.04, 0.06, 0.1, 0.15],
  contactRate: [0.01, 0.02, 0.03, 0.045, 0.06, 0.08, 0.1, 0.12],
  shotHitChance: [0.02, 0.04, 0.06, 0.08, 0.12, 0.16, 0.2],
  focusHitsPerSecond: [0.05, 0.1, 0.15, 0.2, 0.3],
  logbookPickChance: [0, 0.15, 0.3, 0.5],
};

function evaluate(data, gs, model, seeds) {
  const kind = kindOfNew(data);
  const pairs = gs.map(g => ({ g, m: modelSide(simulateRun, data, g, model, seeds, kind) }));
  return { loss: loss(pairs), pairs };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const data = JSON.parse(fs.readFileSync(path.join(here, 'data.json'), 'utf8'));
  const gs = loadSessions().map(x => gameSide(x.s));
  const report = process.argv.includes('--report');
  // Measured straight from the sessions (not searched): island hits that cost hull per minute (damage / the hit's hull).
  const islandRate = gs.slice(1).reduce((a, g) => a + g.island / (data.run.islandImpactDamage || 5), 0) / gs.slice(1).reduce((a, g) => a + g.end / 60, 0);
  let best = Object.fromEntries(Object.keys(KNOBS).map(k => [k, MODEL[k]]));
  best.islandHitsPerMinute = +islandRate.toFixed(2);
  let bestLoss = evaluate(data, gs, best, 40).loss;
  console.log(`start loss ${bestLoss.toFixed(3)} (island hits ${best.islandHitsPerMinute}/min from sessions 2 and 3)`);
  if (!report) {
    for (let round = 0; round < 4; round++) {
      let moved = false;
      for (const [k, values] of Object.entries(KNOBS)) {
        for (const v of values) {
          if (v === best[k]) continue;
          const l = evaluate(data, gs, { ...best, [k]: v }, 40).loss;
          if (l < bestLoss - 1e-4) { bestLoss = l; best = { ...best, [k]: v }; moved = true; }
        }
        console.log(`round ${round + 1} ${k.padEnd(20)} ${String(best[k]).padEnd(6)} loss ${bestLoss.toFixed(3)}`);
      }
      if (!moved) break;
    }
  }
  const final = evaluate(data, gs, best, 200);
  console.log(`\nfinal loss (200 seeds) ${final.loss.toFixed(3)}; constants:`, JSON.stringify(best));
  for (const [i, { g, m }] of final.pairs.entries()) {
    console.log(`\n${SESSIONS[i]}: ${g.sunk ? 'sunk' : 'ended'} at ${(g.end / 60).toFixed(1)} min; model sinks by then (+1 min) in ${(m.sunkBy * 100).toFixed(0)}%`);
    for (let j = 0; j < g.minutes.length; j++) { const a = g.minutes[j], b = m.minutes[j];
      console.log(`  ${String(a.m).padStart(2)}  level ${a.level}|${b.level}  kills ${a.kills}|${b.kills}  hull ${(a.hull * 100).toFixed(0)}|${b.hull != null ? (b.hull * 100).toFixed(0) : '-'}  dmg ${a.dmg.toFixed(0)}|${b.dmg ?? '-'}  dps ${Math.round(a.dps)}|${b.dps ?? '-'}  afloat ${(b.afloat * 100).toFixed(0)}%`); }
    console.log('  fights', JSON.stringify(g.fights), '|', JSON.stringify(m.fights));
  }
  if (!report) fs.writeFileSync(path.join(here, 'calibration-sessions.json'), JSON.stringify({ fitted: new Date().toISOString(), loss: final.loss, model: best }, null, 1));
}
