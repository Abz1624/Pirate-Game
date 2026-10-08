// V5 M9a (I9a.3): the run checks' rules (plausibility rules v1, shadow mode, D23). Generated from the game's real config
// (data.json, the Unity Editor's MonteCarloExport of the config set the game runs on) and the refit simulator (sim.mjs):
// every bound is max(config margin, p99.9 of the strongest player personality's simulated voyages x 1.25). Upper bounds
// on output only (kills, XP, gold, damage dealt, level, chests, points of interest, time survived in overtime), plus
// consistency checks that only ever cap output (level against XP, kills against spawns, hull against damage and heals).
//
// The rules are written to server/RoguePirates.Api/Plausibility/plausibility-rules.json: the API embeds it and checks every
// uploaded run log against it; the game's EditMode tests read the same file (V5PlausibilityRulesTests). The rules
// version goes up by one whenever the rules' content changes; the API then re-scores the stored runs on its next start.
//
//   node plausibility.mjs                 regenerate the rules from data.json (then run the check below)
//   node plausibility.mjs --check         the false-positive budget: 10,000 more strongest-personality voyages (other
//                                         seeds) against the rules; at most 0.1% may be flagged
//   node plausibility.mjs --edge-cases    the shared edge-case fixture (Fixtures/plausibility/edge-cases: minute edges, the
//                                         hull-ledger tolerance, the overtime limit, the level against the XP level): this
//                                         evaluator must give its verdicts, as the API's and the game's tests check theirs
//   node plausibility.mjs --sessions      write the recorded voyages (plan/playtest/sessions) as run logs to the API
//                                         tests' fixtures, with every verdict, and show them against the rules
//
// Config margins and simulator settings: plausibility-margins.json (owner-editable).
// Personality mapping (PlayerPersonalityConfig -> the model, relative to the voyages the model was fitted on, which play
// like the Steady Captain: dodge 0.7, contact avoidance 0.65, pickup greed 0.5):
//   shots landing x (1 - dodgeSkill) / 0.3; contacts x (1 - contactAvoidance) / 0.35; aim x (0.3 + dodgeSkill);
//   barrel pickup x (0.5 + pickupGreed); chests All every 90 s, Some and Evolution 105 s, None never; the boss summoned as
//   the personality does (a minute, the 12:00 hint, the 13:00 marker, never); stops as it does (death, a time, after the
//   boss); the Powder Cache challenge cleared at a random time between 1:30 and 10:00 when it visits caches. Each voyage
//   draws a Captain (equal shares) and (rules v2) its build's reach: engagement between the fitted 0.9 and 1 (every enemy
//   that spawns is fought; the high-kill builds, S6).
//
// Rules v2 (8 Oct): rate bounds never fall over the run (a running max); the build's reach above; the simulated run log's
// spawns count the scripted fights and the boss, as the game's RunLogRecorder does (sim.mjs). D25: the owner's voyages on the current
// build (S5 with today's config hash, S6) must pass the shipped rules; --sessions writes them to expected-verdicts.json
// (currentBuild) with the share of each bound they use.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { simulateRun, MODEL } from './sim.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.join(here, '..', '..', '..');
export const RULES_PATH = path.join(repo, 'server', 'RoguePirates.Api', 'Plausibility', 'plausibility-rules.json');
export const FIXTURES = path.join(repo, 'server', 'RoguePirates.Api.Tests', 'Fixtures', 'plausibility');
const MINUTE = 60, LOG_MAX_MINUTES = 122; // RunLogRules.MaxMinutes (7200 s / 60 + 2)

// ---- personalities -> simulator parameters ---------------------------------------------------------------------------

const REF = { dodge: 0.7, contact: 0.65, greed: 0.5 };
export function personalityParams(data, p, r) {
  const model = {
    shotHitChance: MODEL.shotHitChance * (1 - p.dodgeSkill) / (1 - REF.dodge),
    dodge: 1 - (1 - MODEL.dodge) * (1 - p.contactAvoidance) / (1 - REF.contact),
    accuracy: MODEL.accuracy * (0.3 + p.dodgeSkill),
    lootShare: Math.min(1, MODEL.lootShare * (0.5 + p.pickupGreed)),
    chestEverySeconds: p.chests === 'All' ? 90 : p.chests === 'None' ? 1e9 : 105,
    logbookPickChance: ['Damage', 'Focused'].includes(p.cards) ? 0.25 : ['Economy', 'Defense'].includes(p.cards) ? 0.75 : 0.5,
  };
  const st = data.stage || {};
  const bossAt = p.bossTiming === 'Minute' ? Math.round(p.bossMinute * 60) : p.bossTiming === 'OnHint' ? st.beaconHintSeconds ?? 720
    : p.bossTiming === 'OnMarker' ? st.beaconMarkerSeconds ?? 780 : null;
  const stopAt = p.stop === 'Time' && p.stopMinutes > 0 ? Math.round(p.stopMinutes * 60) : null;
  const stopAfterBoss = p.stop === 'AfterBoss' ? Math.round((p.stopMinutes || 0) * 60) : null;
  const cache = (p.landmarks & 2) !== 0;
  const captains = Object.keys(data.captains);
  const captain = captains[Math.floor(r() * captains.length)];
  const powderCacheAt = cache ? 90 + Math.floor(r() * 510) : null;
  // Rules v2: the build's reach. MODEL.engagement (0.9, fitted on S1-S5) lets a tenth of the wave enemies sail past unkilled;
  // a build that reaches them (S6, 8 Oct: Cursed Doubloons and Squid Ink Pots, enemies alive kept at 1-27) kills what
  // spawns, and replayed at engagement 1 the model gives that voyage's per-minute kills (at 0.9 it stays 15-25% under).
  // Each voyage draws its reach between the two, so the bounds cover the high-kill builds.
  model.engagement = MODEL.engagement + (1 - MODEL.engagement) * r();
  return { model, bossAt, stopAt, stopAfterBoss, powderCacheAt, captain, wreck: (p.landmarks & 1) !== 0 };
}

const lcg = seed => { let s = seed >>> 0; return () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; }; };

/** One simulated voyage of personality p as a run log's minutes (the RunLogBuilder's numbers). */
export function simulateLog(data, p, seed, maxMinutes, baseModel = {}) {
  const pp = personalityParams(data, p, lcg(seed ^ 0x9e3779b9));
  const run = simulateRun(data, { captain: pp.captain, seed, bossAt: pp.bossAt, stopAt: pp.stopAt, stopAfterBoss: pp.stopAfterBoss,
    powderCacheAt: pp.powderCacheAt, model: { ...baseModel, ...pp.model, maxMinutes } });
  const repairAt = MODEL.repairAtSeconds;
  const minutes = run.minutes.map((m, i) => ({
    m: i, seconds: m.seconds, spawns: m.spawns, kills: m.kills, eliteKills: m.eliteKills, bossKills: m.bossKills, gold: m.gold, xp: m.xp,
    level: Math.max(1, m.level), taken: m.taken, dealt: m.dealt, healed: m.healed, pickups: 0, chests: m.chests,
    pois: (pp.wreck && repairAt >= i * MINUTE && repairAt < (i + 1) * MINUTE ? 1 : 0) + (pp.powderCacheAt != null && pp.powderCacheAt >= i * MINUTE && pp.powderCacheAt < (i + 1) * MINUTE ? 1 : 0),
    alive: m.alive, hull: m.hullAbs, maxHull: m.maxHull, wk: [],
  }));
  return { v: 1, configHash: data.meta?.configHash, seconds: run.endSeconds, end: run.died ? 'sunk' : 'quit', minutes, captain: run.captain };
}

// ---- quantiles and bounds --------------------------------------------------------------------------------------------

const quantile = (values, p) => { if (!values.length) return null; const s = Float64Array.from(values).sort(); return s[Math.min(s.length - 1, Math.floor(s.length * p))]; };

/** XP to the next level the way the game counts it (ProgressionMath: the table, then its last steps continued). */
export function xpToNext(table, level) {
  if (level < table.length) return table[level];
  const n = table.length, k = Math.min(4, n - 1), step = Math.max(1, Math.round((table[n - 1] - table[n - 1 - k]) / k));
  return table[n - 1] + step * (level - n + 1);
}
/** The level a run's total XP reaches (from the start level). */
export function levelFromXp(table, startLevel, totalXp) {
  let level = startLevel, left = totalXp;
  while (level < 1000) { const need = xpToNext(table, level); if (left < need) break; left -= need; level++; }
  return level;
}

const FIELDS = ['kills', 'xp', 'gold', 'dealt', 'chests', 'pois'], BARRELS = ['xp', 'gold'];
const RUNNING_MAX = new Set(['kills', 'xp', 'gold', 'dealt']); // rate bounds that never fall (rules v2)
const REASON = { kills: 'kills', xp: 'xp', gold: 'gold', dealt: 'damage_dealt', chests: 'chests', pois: 'points_of_interest' };
const ID = { kills: 'KILLS', xp: 'XP', gold: 'GOLD', dealt: 'DEALT', chests: 'CHESTS', pois: 'POIS' };

function buildRules(data, margins, strongest, logs, screen, baseModel = {}) {
  const Q = margins.quantile, K = margins.multiplier, nMin = margins.minSamples, L = LOG_MAX_MINUTES;
  const table = data.progression.xpToNext, start = data.progression.startLevel ?? 0;
  const chestCap = (data.stage?.salvageChests ?? 12) + (margins.runTotal.chestsExtra ?? 0);
  const deadline = data.timeline.deadline;
  // The bounds of one group of voyages (one Captain's).
  const boundsOf = group => {
    const perMinute = {}, total = {}, level = [];
    // samples per minute index (full minutes for the rates; every minute for the running totals and the level)
    const rate = {}, cum = {}, lv = [];
    for (const f of FIELDS) { rate[f] = []; cum[f] = []; }
    for (const log of group) {
      const run = {}; for (const f of FIELDS) run[f] = 0;
      log.minutes.forEach((m, i) => {
        for (const f of FIELDS) {
          run[f] += m[f];
          (cum[f][i] ??= []).push(run[f]);
          // XP and gold drop as barrels the ship must sail over; barrels left behind are often collected in the next minute
          // (sailing back through a field of them), so their minute bound covers two minutes' drops.
          const carry = BARRELS.includes(f) && i > 0 ? log.minutes[i - 1][f] : 0;
          if (m.seconds >= MINUTE) (rate[f][i] ??= []).push(m[f] + carry);
        }
        (lv[i] ??= []).push(m.level);
      });
    }
    const pooled = (arr, i) => { // the minute's samples, widened to neighbouring minutes while there are too few
      let w = 0, s = arr[i] || [];
      while (s.length < nMin && w < arr.length) { w++; s = []; for (let j = Math.max(0, i - w); j <= Math.min(arr.length - 1, i + w); j++) if (arr[j]) s = s.concat(arr[j]); }
      return s;
    };
    for (const f of FIELDS) {
      const b = [];
      const lastData = rate[f].length - 1;
      for (let i = 0; i < L; i++) {
        const src = pooled(rate[f], Math.min(i, lastData));
        const q = src.length ? quantile(src, Q) : 0;
        // Rules v2: a rate bound never falls as the run goes on (a running max over the minutes). A build's output only grows
        // (damage dealt), and the kills, XP and gold a minute are capped by the spawns, which the simulator's p99.9 follows
        // minute by minute (rules v1: kills 224 at minute 1, 194 at minute 4, 259 at minute 9). A player whose build
        // reached a rate early keeps it: the spawn schedule's dips are not a ceiling (the boundaries do not line up with
        // the waves, a crowd or a horde left alive is cleared a minute later, a strong build kills what a dip lets through,
        // and XP and gold barrels are collected late). S6 (8 Oct) was flagged at minute 4 (200 > 194) after 186 at minute 1.
        b.push(Math.max(margins.perMinute[f], Math.ceil(q * K), RUNNING_MAX.has(f) && i > 0 ? b[i - 1] : 0));
      }
      perMinute[f] = b;
    }
    // running totals: the quantile while there are enough voyages that long, then the last bound plus the minute bound
    for (const f of FIELDS) {
      const b = [];
      for (let i = 0; i < L; i++) {
        const src = cum[f][i] || [];
        const prev = i > 0 ? b[i - 1] : 0;
        let v = src.length >= nMin ? Math.ceil(quantile(src, Q) * K) : prev + perMinute[f][i];
        v = Math.max(v, prev, margins.runTotal[f] ?? 0);
        b.push(v);
      }
      total[f] = b;
    }
    // chests a run: the map's salvage chests plus the Powder Cache chests (config); points of interest a run: the config margin
    total.chests = total.chests.map(() => chestCap);
    total.pois = total.pois.map(() => margins.runTotal.pois);
    for (let i = 0; i < L; i++) {
      const src = lv[i] || [];
      const prev = i > 0 ? level[i - 1] : 0;
      // with too few voyages that long: the level the run-total XP bound reaches
      const v = src.length >= nMin ? Math.ceil(quantile(src, Q) * K) : levelFromXp(table, start, total.xp[i]);
      level.push(Math.max(v, prev, margins.level));
    }
    // overtime survival: seconds past the deadline among the voyages that reached it
    const over = group.filter(l => l.seconds > deadline).map(l => l.seconds - deadline);
    return { perMinute, total, level, over, overQ: over.length ? quantile(over, Q) : 0 };
  };
  // Each Captain's voyages give their own bounds (their weapons differ: the Flamethrower's damage has the longest tail);
  // a run's bound is the highest of them.
  const groups = {};
  for (const log of logs) (groups[log.captain] ??= []).push(log);
  const each = Object.values(groups).map(boundsOf);
  const maxOf = pick => pick(each[0]).map((_, i) => Math.max(...each.map(e => pick(e)[i])));
  const perMinute = {}, total = {};
  for (const f of FIELDS) { perMinute[f] = maxOf(e => e.perMinute[f]); total[f] = maxOf(e => e.total[f]); }
  const level = maxOf(e => e.level);
  const over = each.flatMap(e => e.over), overQ = Math.max(...each.map(e => e.overQ));
  const overtimeBound = Math.max(margins.overtimeSeconds, Math.ceil(overQ * K));

  const rules = [];
  for (const f of FIELDS) rules.push({ id: `PR-${ID[f]}-MIN`, kind: 'minute', field: f, reason: `${REASON[f]}_per_minute`, configMargin: margins.perMinute[f], bounds: perMinute[f] });
  for (const f of FIELDS) rules.push({ id: `PR-${ID[f]}-RUN`, kind: 'total', field: f, reason: `${REASON[f]}_run_total`,
    configMargin: f === 'chests' ? chestCap : margins.runTotal[f], bounds: total[f] });
  rules.push({ id: 'PR-LEVEL', kind: 'level', field: 'level', reason: 'level', configMargin: margins.level, bounds: level });
  rules.push({ id: 'PR-LEVEL-XP', kind: 'levelFromXp', field: 'level', reason: 'level_above_xp', tolerance: 0 });
  rules.push({ id: 'PR-KILLS-SPAWNS', kind: 'killsWithinSpawns', field: 'kills', reason: 'kills_above_spawns', margin: margins.killsOverSpawns });
  rules.push({ id: 'PR-HULL', kind: 'hullLedger', field: 'hull', reason: 'hull_above_damage_and_heals', absolute: margins.hullLedger.absolute, fractionOfMaxHull: margins.hullLedger.fractionOfMaxHull });
  rules.push({ id: 'PR-OVERTIME', kind: 'overtimeSurvival', field: 'seconds', reason: 'overtime_survival', deadline, bound: overtimeBound, configMargin: margins.overtimeSeconds });

  return {
    format: 'rp-plausibility-rules',
    rulesVersion: 0, // set by main()
    mode: 'shadow',
    logFormat: 'rp-runlog', logVersion: 1, minuteSeconds: MINUTE,
    configHash: data.meta?.configHash, configHashes: [data.meta?.configHash], setId: data.meta?.setId,
    xpToNext: table, startLevel: start,
    margin: { quantile: Q, multiplier: K, minSamples: nMin, note: 'bound = max(config margin, ceil(p99.9 of the strongest personality x 1.25)); kills, XP, gold and damage dealt a minute: a running max (never below an earlier minute bound); XP and gold minutes: the p99.9 of two minutes of drops (barrels collected a minute late); late minutes with too few voyages use neighbouring minutes (rates) or add the minute bound to the last total; each voyage draws the reach of its build (engagement) between the fitted 0.9 and 1' },
    simulator: { file: 'plan/playtest/montecarlo/sim.mjs', data: 'plan/playtest/montecarlo/data.json', exported: data.meta?.exported, personality: strongest.id,
      runs: logs.length, maxMinutes: margins.maxMinutes, seeds: `${SEED_RULES} + i * 7919`, screen, modelOverrides: Object.keys(baseModel).length ? baseModel : undefined,
      captains: Object.fromEntries(Object.entries(groups).map(([k, v]) => [k, v.length])), boundPerCaptain: 'the highest of the Captains\' bounds',
      overtime: { voyagesReachingIt: over.length, p999Seconds: overQ } },
    rules,
  };
}

// ---- the evaluator (the API's PlausibilityChecker and the game's test copy do the same) ---------------------------------

/** status: pass, flagged or outdated_client; reasons: one per broken rule (its first minute). */
export function evaluate(rules, log, { checkConfig = true } = {}) {
  if (!log) return { status: 'outdated_client', reasons: [{ code: 'no_log', rule: 'PR-VERSION' }] };
  if (log.v !== rules.logVersion) return { status: 'outdated_client', reasons: [{ code: 'log_version', rule: 'PR-VERSION', value: log.v }] };
  if (checkConfig && !rules.configHashes.includes(log.configHash)) return { status: 'outdated_client', reasons: [{ code: 'config_not_in_rules', rule: 'PR-CONFIG' }] };
  const reasons = [], hit = new Set();
  const flag = (r, minute, value, bound) => { if (hit.has(r.id)) return; hit.add(r.id); reasons.push({ code: r.reason, rule: r.id, minute, value: round1(value), bound: round1(bound) }); };
  const at = (b, i) => b[Math.min(i, b.length - 1)];
  const run = { kills: 0, xp: 0, gold: 0, dealt: 0, chests: 0, pois: 0, spawns: 0 };
  log.minutes.forEach((m, i) => {
    for (const f of [...FIELDS, 'spawns']) run[f] += m[f];
    const prev = i > 0 ? log.minutes[i - 1] : null;
    for (const r of rules.rules) {
      switch (r.kind) {
        case 'minute': if (m[r.field] > at(r.bounds, i)) flag(r, i, m[r.field], at(r.bounds, i)); break;
        case 'total': if (run[r.field] > at(r.bounds, i)) flag(r, i, run[r.field], at(r.bounds, i)); break;
        case 'level': if (m.level > at(r.bounds, i)) flag(r, i, m.level, at(r.bounds, i)); break;
        case 'levelFromXp': { const l = Math.max(1, levelFromXp(rules.xpToNext, rules.startLevel, run.xp)) + (r.tolerance || 0); if (m.level > l) flag(r, i, m.level, l); break; }
        case 'killsWithinSpawns': if (run.kills > run.spawns + r.margin) flag(r, i, run.kills, run.spawns + r.margin); break;
        case 'hullLedger': if (prev) {
          const expected = Math.max(0, Math.min(m.maxHull, prev.hull + m.healed - m.taken + Math.max(0, m.maxHull - prev.maxHull)));
          const allowed = expected + r.absolute + r.fractionOfMaxHull * m.maxHull;
          if (m.hull > allowed) flag(r, i, m.hull, allowed);
        } break;
      }
    }
  });
  for (const r of rules.rules) if (r.kind === 'overtimeSurvival' && log.seconds - r.deadline > r.bound) flag(r, log.minutes.length - 1, log.seconds - r.deadline, r.bound);
  return { status: reasons.length ? 'flagged' : 'pass', reasons };
}
const round1 = x => x == null ? x : Math.round(x * 10) / 10;

// ---- main ----------------------------------------------------------------------------------------------------------------

const SEED_RULES = 1000003, SEED_CHECK = 7000001, SEED_SCREEN = 424242;
const seedOf = (base, i) => (base + i * 7919) >>> 0;

function strongestOf(data, margins, baseModel = {}) {
  const ps = data.personalities || [];
  if (!ps.length) throw new Error('data.json has no personalities: re-export it from the Editor (MonteCarloExport)');
  const screen = ps.map(p => {
    const kills = [];
    for (let i = 0; i < margins.screenRuns; i++) { const l = simulateLog(data, p, seedOf(SEED_SCREEN, i), margins.maxMinutes, baseModel); kills.push(l.minutes.reduce((s, m) => s + m.kills, 0)); }
    return { id: p.id, medianKills: quantile(kills, 0.5), p99Kills: quantile(kills, 0.99) };
  }).sort((a, b) => b.medianKills - a.medianKills);
  return { strongest: ps.find(p => p.id === screen[0].id), screen };
}

function readJson(p) { return JSON.parse(fs.readFileSync(p, 'utf8')); }

/** The rules for one config (data) and model settings: the strongest personality's voyages, then the bounds. */
export function makeRules(data, margins, baseModel = {}) {
  const { strongest, screen } = strongestOf(data, margins, baseModel);
  const logs = [];
  for (let i = 0; i < margins.runs; i++) logs.push(simulateLog(data, strongest, seedOf(SEED_RULES, i), margins.maxMinutes, baseModel));
  return buildRules(data, margins, strongest, logs, screen, baseModel);
}

function generate() {
  const data = readJson(path.join(here, 'data.json')), margins = readJson(path.join(here, 'plausibility-margins.json'));
  const t0 = Date.now();
  const rules = makeRules(data, margins);
  console.log('screen (median kills a voyage):', rules.simulator.screen.map(s => `${s.id} ${s.medianKills}`).join(', '));
  const strongest = { id: rules.simulator.personality }, logs = { length: rules.simulator.runs };
  // the version: unchanged when the content is the same, else one more than the file's
  let old = null; try { old = readJson(RULES_PATH); } catch { /* first run */ }
  const content = r => JSON.stringify({ ...r, rulesVersion: 0, generated: null, checks: null, simulator: { ...r.simulator, exported: null } });
  rules.rulesVersion = old && content(old) === content(rules) ? old.rulesVersion : (old?.rulesVersion ?? 0) + 1;
  rules.generated = old && old.rulesVersion === rules.rulesVersion ? old.generated : new Date().toISOString();
  rules.checks = old && old.rulesVersion === rules.rulesVersion ? old.checks : null;
  fs.mkdirSync(path.dirname(RULES_PATH), { recursive: true });
  fs.writeFileSync(RULES_PATH, stringify(rules));
  console.log(`rules v${rules.rulesVersion} for config ${String(rules.configHash).slice(0, 10)} from ${logs.length} ${strongest.id} voyages (${((Date.now() - t0) / 1000).toFixed(0)} s): ${RULES_PATH}`);
  for (const r of rules.rules) console.log(`  ${r.id.padEnd(16)} ${r.reason.padEnd(30)} ${r.bounds ? 'min 0-4: ' + r.bounds.slice(0, 5).join(' ') + ' | 9: ' + r.bounds[9] + ' | 14: ' + r.bounds[14] + ' | 19: ' + r.bounds[19] + ' | 29: ' + r.bounds[29] : JSON.stringify({ ...r, id: undefined, kind: undefined, reason: undefined, field: undefined })}`);
}

/** Compact JSON: one line per rule's bounds. */
function stringify(rules) {
  return JSON.stringify(rules, (k, v) => v, 1).replace(/\[\s+([-\d.,\s]+?)\s+\]/g, (m, inner) => '[' + inner.replace(/\s+/g, '') + ']') + '\n';
}

function check() {
  const data = readJson(path.join(here, 'data.json')), margins = readJson(path.join(here, 'plausibility-margins.json')), rules = readJson(RULES_PATH);
  if (rules.configHash !== data.meta?.configHash) throw new Error('the rules are for another config: run node plausibility.mjs first');
  const p = data.personalities.find(x => x.id === rules.simulator.personality);
  const n = margins.checkRuns, byRule = {};
  let flagged = 0;
  const t0 = Date.now();
  for (let i = 0; i < n; i++) {
    const log = simulateLog(data, p, seedOf(SEED_CHECK, i), margins.maxMinutes);
    const v = evaluate(rules, log);
    if (v.status !== 'pass') { flagged++; for (const r of v.reasons) byRule[r.rule] = (byRule[r.rule] ?? 0) + 1; }
  }
  const share = flagged / n;
  const result = { runs: n, personality: p.id, seeds: `${SEED_CHECK} + i * 7919`, flagged, share, budget: 0.001, pass: share <= 0.001, byRule,
    command: 'node plan/playtest/montecarlo/plausibility.mjs --check', at: new Date().toISOString(), seconds: Math.round((Date.now() - t0) / 1000) };
  rules.checks = { ...(rules.checks || {}), falsePositive: result };
  fs.writeFileSync(RULES_PATH, stringify(rules));
  console.log(`false positives: ${flagged} of ${n} ${p.id} voyages flagged (${(share * 100).toFixed(3)}%; budget 0.1%) ${result.pass ? 'PASS' : 'FAIL'}`, JSON.stringify(byRule));
  if (!result.pass) process.exitCode = 1;
}

// ---- recorded voyages as run logs -------------------------------------------------------------------------------------

/**
 * A recorded session (rp-session-1, plan/playtest/sessions) as an rp-runlog v1: the 10 s samples give each minute's kills,
 * XP (rebuilt from the level and the XP bar with the session's own XP table), gross gold (the gold in hand plus the paid
 * chests' prices), damage taken, heals, hull and enemies alive; events give chests and landmarks, the focus list the
 * elite and boss kills. Not in a session file, so approximated: spawns (kills plus the change in enemies alive, so never
 * below the kills), damage dealt (the weapons' totals spread over the minutes by the theoretical DPS) and pickups (0).
 */
export function sessionToLog(s, sd) {
  const H = s.header, S = s.samples, end = H.endTime, table = sd.progression.xpToNext, startLevel = sd.progression.startLevel ?? 0;
  const at = t => { let b = S[0]; for (const x of S) if (x.t <= t + 1e-6) b = x; return b; };
  const totalXp = x => { let sum = 0; for (let l = startLevel; l < x.level; l++) sum += xpToNext(table, l); return sum + x.xp; };
  const st = sd.stage || {};
  const price = k => k <= 0 ? st.chestBasePrice ?? 20 : Math.ceil((st.chestPriceIncrementBase ?? 45) * Math.pow(st.chestPriceGrowth ?? 1.35, k));
  // paid chests: a ChestOpened at a SalvageChest point of interest (the Powder Cache's chest is free)
  const paid = []; let k = 0;
  for (const e of s.events) if (e.type === 'chest' && s.events.some(x => x.type === 'poi' && x.id === 'SalvageChest' && Math.abs(x.t - e.t) < 0.01)) paid.push({ t: e.t, price: price(k++) });
  const spentBy = t => paid.filter(p => p.t <= t + 1e-6).reduce((a, p) => a + p.price, 0);
  const grossAt = t => at(t).gold + spentBy(at(t).t);
  const dealtTotal = Object.values(s.weapons || {}).reduce((a, w) => a + w.damage, 0);
  const dpsInt = (a, b) => { let sum = 0; for (let i = 1; i < S.length; i++) { const lo = Math.max(a, S[i - 1].t), hi = Math.min(b, S[i].t); if (hi > lo) sum += S[i - 1].dps * (hi - lo); } return sum; };
  const allDps = dpsInt(0, end) || 1;
  const count = Math.max(1, Math.ceil(end / MINUTE - 1e-9));
  const minutes = [];
  for (let i = 0; i < count; i++) {
    const a = i * MINUTE, b = Math.min(end, (i + 1) * MINUTE), A = i === 0 ? S[0] : at(a), B = at(b);
    const kills = B.kills - A.kills;
    const inWin = e => e.t > a && e.t <= b + 1e-6 && (i > 0 || e.t >= 0);
    const focusKills = kind => (s.focus || []).filter(f => f.kind === kind && f.kill >= 0 && f.kill > a && f.kill <= b + 1e-6).length;
    minutes.push({
      m: i, spawns: Math.max(kills, kills + B.enemies - A.enemies), kills, eliteKills: Math.min(kills, focusKills('elite')), bossKills: Math.min(kills, focusKills('boss')),
      gold: Math.max(0, (i === 0 ? grossAt(b) : grossAt(b) - grossAt(a))), xp: totalXp(B) - (i === 0 ? 0 : totalXp(A)), level: Math.max(1, B.level),
      taken: r1(B.damageTaken - (i === 0 ? 0 : A.damageTaken)), dealt: r1(dealtTotal * dpsInt(a, b) / allDps), healed: r1(B.healed - (i === 0 ? 0 : A.healed)),
      pickups: 0, chests: s.events.filter(e => e.type === 'chest' && inWin(e)).length, pois: s.events.filter(e => e.type === 'landmark' && inWin(e)).length,
      alive: B.enemies, hull: r1(B.hull), maxHull: r1(B.maxHull), wk: [],
    });
  }
  return {
    format: 'rp-runlog', v: 1, seed: H.seed || 1, seedSource: 'local', issuedRunId: null, runId: path.basename(String(H.startedAt || 'session')).replace(/[^A-Za-z0-9_.:-]/g, '-'),
    configHash: H.configHash, balanceVersion: H.balanceVersion ?? null, contentHash: H.contentHash ?? null, stageProfile: H.stageProfile ?? null,
    buildId: `${H.appVersion || '0'}+session`, captain: H.captain, startingWeapon: H.startingWeapon, editor: !!H.editor, developmentBuild: !!H.developmentBuild,
    end: H.endReason || 'quit', seconds: r1(end), bossSummoned: H.bossSummoned >= 0 ? r1(H.bossSummoned) : -1, bossDefeated: H.bossDefeated >= 0 ? r1(H.bossDefeated) : -1,
    minuteSeconds: MINUTE, weapons: [], minutes, inputEvery: 5, inputCols: ['t', 'x', 'z', 'heading', 'steer', 'throttle', 'boost'], inputs: [],
  };
}
const r1 = x => Math.round(x * 10) / 10;

/**
 * D25: the owner's voyages on the current build, which must pass the shipped rules. S5 was played on daa47525, which
 * differs from today's config only in the Unlocks asset (no rule reads it), so it is checked as a log naming today's config
 * (the API tests' TodaysVoyage does the same); S6 was played on today's config.
 */
export const CURRENT_BUILD = [
  { id: 'S5', session: 'session-20261008-001147-rook.json', convert: true },
  { id: 'S6', session: 'session-20261008-105707-rook.json', convert: false, note: 'M9a playtest, 8 Oct: Cursed Doubloons 692 kills, Squid Ink Pots 320; sunk at 8:36' },
];

/** The largest share of each bounded rule's bound a log uses (per minute or running total), and the minute it happens. */
function headroom(rules, log) {
  const head = {};
  for (const r of rules.rules.filter(r => r.bounds)) {
    let run = 0, worst = 0, at = 0;
    log.minutes.forEach((m, i) => { const v = r.kind === 'total' ? (run += m[r.field]) : m[r.field], b = r.bounds[Math.min(i, r.bounds.length - 1)]; if (v / b > worst) { worst = v / b; at = i; } });
    head[r.id] = { used: Math.round(worst * 1000) / 10, minute: at };
  }
  return head;
}

async function sessions() {
  const { SESSIONS, loadSessionData } = await import('./session_data.mjs');
  const rules = readJson(RULES_PATH), data = readJson(path.join(here, 'data.json'));
  const dir = path.join(here, '..', 'sessions');
  fs.mkdirSync(FIXTURES, { recursive: true });
  const margins = readJson(path.join(here, 'plausibility-margins.json'));
  const expected = [], ownRules = {};
  const files = fs.readdirSync(dir).filter(f => f.startsWith('session-') && f.endsWith('.json')).sort();
  for (const file of files) {
    const s = readJson(path.join(dir, file));
    // the fitted voyage played on the same build (the same config hash): its config snapshot and model settings
    const known = SESSIONS.find(x => x.file === file) || SESSIONS.find(x => String(s.header.configHash).startsWith(x.configHash));
    const current = CURRENT_BUILD.find(x => x.session === file);
    const snap = known ? loadSessionData(known) : { data, model: {} };
    const sd = { ...snap.data, personalities: snap.data.personalities || data.personalities };
    const log = sessionToLog(s, sd);
    const out = file.replace(/^session-/, 'session-log-');
    fs.writeFileSync(path.join(FIXTURES, out), JSON.stringify(log) + '\n');
    // the rules of the config it was played on (generated the same way, written next to the logs); a voyage on the shipped
    // config (S6) has the shipped rules as its own
    const key = known ? known.id : current ? current.id : 'today';
    if (!ownRules[key] && s.header.configHash === rules.configHash) {
      ownRules[key] = { ...rules, note: `test fixture: the shipped rules (v${rules.rulesVersion}): ${key} was played on the config they were made for` };
      fs.writeFileSync(path.join(FIXTURES, `rules-${key}.json`), stringify(ownRules[key]));
    }
    if (!ownRules[key]) {
      const t0 = Date.now();
      const own = makeRules({ ...sd, meta: { ...(sd.meta || {}), configHash: s.header.configHash } }, { ...margins, runs: margins.fixtureRuns ?? margins.runs }, snap.model);
      own.rulesVersion = rules.rulesVersion; own.generated = rules.generated;
      own.note = `test fixture: the rules for the config ${key} was played on (its session-data snapshot), made the way the shipped rules are`;
      ownRules[key] = own;
      fs.writeFileSync(path.join(FIXTURES, `rules-${key}.json`), stringify(own));
      console.log(`rules for ${key} (${String(s.header.configHash).slice(0, 8)}) in ${((Date.now() - t0) / 1000).toFixed(0)} s`);
    }
    const v = evaluate(rules, log), bounds = evaluate(rules, log, { checkConfig: false }), mine = evaluate(ownRules[key], log);
    expected.push({ file: out, session: file, id: known?.id ?? current?.id ?? null, rules: `rules-${key}.json`, ownVerdict: mine.status, ownReasons: mine.reasons,
      verdict: v.status, reasons: v.reasons.map(r => r.code), boundsVerdict: bounds.status, boundsReasons: bounds.reasons });
    console.log(`    own config's rules: ${mine.status}${mine.reasons.length ? ' ' + JSON.stringify(mine.reasons) : ''}`);
    const tot = f => log.minutes.reduce((a, m) => a + m[f], 0);
    console.log(`${(known?.id ?? current?.id ?? '--').padEnd(3)} ${file.padEnd(36)} ${String(log.seconds).padStart(6)} s  kills ${tot('kills')}  xp ${tot('xp')}  gold ${tot('gold')}  level ${log.minutes.at(-1).level}  -> ${v.status}; bounds only: ${bounds.status}${bounds.reasons.length ? ' ' + JSON.stringify(bounds.reasons) : ''}`);
    // headroom: the largest share of a bound used, per rule
    console.log('      most of a bound used:', JSON.stringify(Object.fromEntries(Object.entries(headroom(rules, log)).map(([k, h]) => [k, Math.round(h.used) + '%']))));
  }
  // D25: the current build's voyages against the shipped rules (S5 converted to today's config hash)
  const currentBuild = [];
  for (const c of CURRENT_BUILD) {
    const row = expected.find(e => e.session === c.session);
    if (!row) throw new Error(`${c.session}: not in plan/playtest/sessions`);
    const log = readJson(path.join(FIXTURES, row.file));
    if (c.convert) log.configHash = rules.configHash;
    const v = evaluate(rules, log), head = headroom(rules, log);
    currentBuild.push({ id: c.id, file: row.file, convertedToShippedConfig: c.convert, verdict: v.status, reasons: v.reasons, headroom: head });
    console.log(`D25 ${c.id} ${row.file}${c.convert ? ' (as today\'s config)' : ''}: ${v.status}${v.reasons.length ? ' ' + JSON.stringify(v.reasons) : ''}`);
    console.log('      % of bound used (minute):', Object.entries(head).map(([k, h]) => `${k} ${h.used}% (${h.minute})`).join(', '));
    if (v.status !== 'pass') process.exitCode = 1;
  }
  fs.writeFileSync(path.join(FIXTURES, 'expected-verdicts.json'), JSON.stringify({ rulesVersion: rules.rulesVersion, configHash: rules.configHash, sessions: expected, currentBuild }, null, 1) + '\n');
  console.log('wrote', FIXTURES);
}

/** V5 M9a (review): the shared edge-case fixture; returns the mismatches (empty when every verdict and reason agrees). */
export function edgeCases(dir = path.join(FIXTURES, 'edge-cases')) {
  const spec = readJson(path.join(dir, 'cases.json')), rules = readJson(path.join(dir, spec.rules)), bad = [];
  for (const c of spec.cases) {
    const v = evaluate(rules, readJson(path.join(dir, c.file)));
    const got = v.reasons.map(r => ({ rule: r.rule, minute: r.minute, value: r.value, bound: r.bound }));
    const ok = v.status === c.expect && got.length === c.reasons.length &&
      c.reasons.every((e, i) => e.rule === got[i].rule && e.minute === got[i].minute && e.value === got[i].value && e.bound === got[i].bound);
    console.log(`${ok ? 'ok  ' : 'FAIL'} ${c.name.padEnd(36)} ${v.status}${got.length ? ' ' + JSON.stringify(got) : ''}`);
    if (!ok) bad.push({ name: c.name, expected: { status: c.expect, reasons: c.reasons }, got: { status: v.status, reasons: got } });
  }
  return bad;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const a = process.argv.slice(2);
  if (a.includes('--check')) check();
  else if (a.includes('--edge-cases')) { const bad = edgeCases(); if (bad.length) { console.error(JSON.stringify(bad, null, 1)); process.exit(1); } console.log('edge cases: all agree'); }
  else if (a.includes('--sessions')) await sessions();
  else generate();
}
