// Playtest (6 Oct, owner: "balance simulators are way different from the gameplay sessions. let me play a session, then
// use that data to verify if the simulations are accurate"). Runs the Monte Carlo model (sim.mjs on data.json) for the
// recorded session's Captain and starting weapon over many seeds and prints the session beside the model: level, kills,
// hull and damage per minute, level-up times, elite / miniboss / boss fights, damage by source, the end; then the
// biggest mismatches and which MODEL constants move them (measured: each constant +-30% on a smaller batch).
//
//   node compare_session.mjs <session.json> [--seeds 200] [--set accuracy=0.5 ...] [--export]
//
// --export first runs `python export_data.py` (data.json from the Unity assets); without it the script warns when the
// assets are newer than data.json. Sessions come from the game (Telemetry/SessionRecorder.cs): plan/playtest/sessions/.
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';
import { simulateRun, MODEL } from './sim.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const opt = { seeds: 200, set: {}, exportData: false, file: null };
for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (a === '--seeds') opt.seeds = +args[++i];
  else if (a === '--set') { const [k, v] = args[++i].split('='); opt.set[k] = +v; }
  else if (a === '--export') opt.exportData = true;
  else opt.file = a;
}
if (!opt.file) { console.log('usage: node compare_session.mjs <session.json> [--seeds 200] [--set key=value] [--export]'); process.exit(1); }

// ---- data.json: fresh? ----
const dataPath = path.join(here, 'data.json');
const assets = path.join(here, '..', '..', '..', 'Pirate Game Unity', 'Assets', '_Game', 'V4', 'Data');
const newestAsset = dir => { let n = 0; if (!fs.existsSync(dir)) return 0; for (const e of fs.readdirSync(dir, { withFileTypes: true })) { const p = path.join(dir, e.name); if (e.isDirectory()) n = Math.max(n, newestAsset(p)); else if (e.name.endsWith('.asset')) n = Math.max(n, fs.statSync(p).mtimeMs); } return n; };
if (opt.exportData) {
  console.log('Regenerating data.json: python export_data.py');
  const r = spawnSync('python', ['export_data.py'], { cwd: here, stdio: 'inherit' });
  if (r.status !== 0) { console.log('export_data.py failed; using the existing data.json.'); }
} else if (newestAsset(assets) > fs.statSync(dataPath).mtimeMs) {
  console.log('WARNING: Unity data assets are newer than data.json. Run with --export (or `python export_data.py`) first.');
}
const data = JSON.parse(fs.readFileSync(dataPath));
const session = JSON.parse(fs.readFileSync(opt.file));
const H = session.header || {};

// ---- helpers ----
const sorted = a => a.filter(x => x != null && !Number.isNaN(x)).sort((x, y) => x - y);
const q = (a, p) => { const s = sorted(a); return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : null; };
const med = a => q(a, 0.5);
const clock = s => s == null || s < 0 ? '-' : `${Math.floor(s / 60)}:${String(Math.round(s % 60)).padStart(2, '0')}`;
const f = (x, d = 0) => x == null || Number.isNaN(x) ? '-' : (+x).toFixed(d);
const pad = (s, n) => String(s).padEnd(n);
const lpad = (s, n) => String(s).padStart(n);
const range = (a, d = 0) => { const s = sorted(a); return s.length ? `${f(q(s, 0.5), d)} (${f(q(s, 0.1), d)}-${f(q(s, 0.9), d)})` : '-'; };

// ---- the session ----
const captain = data.captains[H.captain] ? H.captain : Object.keys(data.captains)[0];
const startWeapon = data.weapons[H.startingWeapon] ? H.startingWeapon : data.captains[captain].weapon;
const end = H.endTime || 0;
const samples = session.samples || [];
const sampleAt = t => { let best = null; for (const s of samples) if (s.t <= t + 0.01) best = s; return best; };
const events = session.events || [];
const bossAt = H.bossSummoned != null && H.bossSummoned >= 0 ? Math.round(H.bossSummoned) : null;

console.log(`\nSession  ${opt.file}`);
console.log(`  ${H.captain} (${H.startingWeapon}), seed ${H.seed}, ${H.platform}${H.editor ? ' editor' : ''}, balance ${H.balanceVersion || '-'}, config ${String(H.configHash || '').slice(0, 10)}`);
console.log(`  ended: ${H.endReason} at ${clock(end)}, level ${H.finalLevel}, ${H.finalKills} kills; boss summoned ${clock(H.bossSummoned)}, defeated ${clock(H.bossDefeated)}`);
if (!data.captains[H.captain]) console.log(`  (captain "${H.captain}" is not in data.json; using ${captain})`);
console.log(`Model    sim.mjs on data.json, ${opt.seeds} seeds, boss summoned ${bossAt == null ? 'never (the session did not summon it)' : 'at ' + clock(bossAt) + ' (the session\'s Beacon summon)'}`);
if (Object.keys(opt.set).length) console.log(`  overrides: ${JSON.stringify(opt.set)}`);

// ---- the model ----
const batch = (model, n, maxMinutes) => {
  const runs = [];
  for (let i = 0; i < n; i++) runs.push(simulateRun(data, { captain, startWeapon, seed: (i + 1) * 7919 + 17, bossAt, model: { ...opt.set, ...model, ...(maxMinutes ? { maxMinutes } : {}) } }));
  return runs;
};
const full = batch({}, opt.seeds);
const endMinutes = Math.max(1 / 60, end / 60);
const cut = batch({}, opt.seeds, endMinutes); // to the session's end: damage by source, chests, totals

// ---- per minute ----
const lastMinute = Math.floor(end / 60);
const simCumKills = (r, m) => r.minutes.filter(x => x.m <= m).reduce((s, x) => s + x.kills, 0);
const simCumDmg = (r, m) => r.minutes.filter(x => x.m <= m).reduce((s, x) => s + x.dmg, 0);
console.log('\nPer minute (game | model median (p10-p90); model rows are the runs still afloat)');
console.log(`${pad('min', 4)}${pad('level', 22)}${pad('kills', 24)}${pad('hull %', 22)}${pad('damage taken (cum.)', 26)}afloat`);
const perMinute = [];
for (let m = 1; m <= Math.min(lastMinute, MODEL.maxMinutes); m++) {
  const s = sampleAt(m * 60);
  const rows = full.filter(r => r.minutes.some(x => x.m === m && x.hull > 0));
  const row = { m, g: s, level: rows.map(r => r.minutes.find(x => x.m === m).level), kills: rows.map(r => simCumKills(r, m)),
    hull: rows.map(r => r.minutes.find(x => x.m === m).hull * 100), dmg: rows.map(r => simCumDmg(r, m)), afloat: rows.length / full.length };
  perMinute.push(row);
  console.log(`${pad(m, 4)}${pad(`${s ? s.level : '-'} | ${range(row.level)}`, 22)}${pad(`${s ? s.kills : '-'} | ${range(row.kills)}`, 24)}` +
    `${pad(`${s && s.maxHull ? f(s.hull / s.maxHull * 100) : '-'} | ${range(row.hull)}`, 22)}${pad(`${s ? f(s.damageTaken) : '-'} | ${range(row.dmg)}`, 26)}${f(row.afloat * 100)}%`);
}
if (!lastMinute) console.log('  (the session is shorter than a minute)');

// ---- level-up times ----
const gameLevelAt = {}; // level -> first time reached
for (const e of events) if (e.type === 'level' && e.n) for (let l = 2; l <= e.n; l++) if (gameLevelAt[l] == null) gameLevelAt[l] = e.t;
for (const s of samples) for (let l = 2; l <= s.level; l++) if (gameLevelAt[l] == null) gameLevelAt[l] = s.t;
console.log('\nFirst level-ups (time reached: game | model median (p10-p90))');
const lvRows = [];
for (const l of [2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30]) {
  const simT = full.map(r => r.levelAt[l - 2]).filter(x => x != null);
  if (gameLevelAt[l] == null && simT.length < full.length * 0.5) continue;
  const reached = simT.length / full.length;
  lvRows.push({ l, g: gameLevelAt[l], sim: simT });
  console.log(`  level ${lpad(l, 2)}: ${lpad(clock(gameLevelAt[l]), 6)} | ${clock(med(simT))} (${clock(q(simT, 0.1))}-${clock(q(simT, 0.9))})${reached < 0.99 ? `, ${f(reached * 100)}% of runs reach it` : ''}`);
}

// ---- focus fights ----
// The model names a scheduled fight by its timeline phase id and a kill band elite 'elite_' + enemy id (the two can
// collide: a phase is told apart by its start time).
const phaseOf = (id, start) => data.timeline.focus.find(x => x.id === id && (start == null || x.t === start));
const simKind = (id, start) => {
  if (id === 'barnacle_king' || (data.enemies[id] && data.enemies[id].boss)) return 'boss';
  const phase = phaseOf(id, start);
  if (phase) return data.enemies[phase.enemy] && data.enemies[phase.enemy].miniboss ? 'miniboss' : 'elite';
  return id.startsWith('elite_') ? 'adaptive elite' : 'elite';
};
const simEnemy = (id, start) => { const phase = phaseOf(id, start); return phase ? phase.enemy : id.startsWith('elite_') && !data.enemies[id] ? id.slice(6) : id; };
const gameFights = {}, simFights = {};
for (const fo of session.focus || []) {
  const kind = fo.kind === 'elite' && fo.adaptive ? 'adaptive elite' : fo.kind;
  (gameFights[kind] ??= []).push({ id: fo.id, spawn: fo.spawn, secs: fo.kill >= 0 ? fo.kill - fo.spawn : null, hp: fo.maxHp, despawn: fo.despawn });
}
for (const r of cut) for (const [id, start, secs, hp] of r.focus) (simFights[simKind(id, start)] ??= []).push({ id: simEnemy(id, start), start, secs, hp });
console.log('\nElite / miniboss / boss fights to the session end (count per run, seconds to kill, HP at spawn: game | model)');
const fightRows = [];
for (const kind of ['miniboss', 'elite', 'adaptive elite', 'boss']) {
  const g = gameFights[kind] || [], s = simFights[kind] || [];
  if (!g.length && !s.length) continue;
  const gSecs = g.map(x => x.secs).filter(x => x != null), sSecs = s.map(x => x.secs);
  fightRows.push({ kind, g: med(gSecs), sim: sSecs });
  console.log(`  ${pad(kind, 15)} count ${g.length} | ${f(s.length / cut.length, 1)}   kill ${f(med(gSecs), 1)}s | ${range(sSecs, 1)}s   HP ${f(med(g.map(x => x.hp)))} | ${f(med(s.map(x => x.hp)))}` +
    (g.some(x => x.secs == null) ? `   (${g.filter(x => x.secs == null).length} not killed in the game)` : ''));
  for (const x of g) console.log(`      game ${pad(x.id, 22)} spawn ${clock(x.spawn)}  ${x.secs != null ? 'killed in ' + f(x.secs, 1) + 's' : x.despawn >= 0 ? 'gone at ' + clock(x.despawn) : 'alive at the end'}  HP ${f(x.hp)}`);
}

// ---- damage by source ----
const gameBy = (session.damageTaken && session.damageTaken.bySource) || {};
const gameTotal = Object.values(gameBy).reduce((s, x) => s + x.damage, 0);
const notModelledKeys = ['island', 'storm', 'stage_hit', 'hazard', 'other'];
const simBy = {}; let simRaw = 0;
for (const r of cut) for (const [id, v] of Object.entries(r.damageBy)) { const k = simEnemy(id); simBy[k] = (simBy[k] ?? 0) + v; simRaw += v; }
const simTotals = cut.map(r => r.damageTaken);
const unmodelled = Object.entries(gameBy).filter(([k]) => notModelledKeys.includes(k)).reduce((s, [, v]) => s + v.damage, 0);
const modelledShare = gameTotal > 0 ? (gameTotal - unmodelled) / gameTotal : 1;
console.log(`\nDamage taken to the session end: game ${f(gameTotal)} (${f(gameTotal * modelledShare)} from enemies and the boss) | model ${range(simTotals)}`);
console.log('  by source, share of the total (game | model; the model\'s shares are raw hits before its per-minute damage curve)');
const notModelled = { island: 'island impacts', storm: 'the storm', stage_hit: 'stage hits (storm hit mode)', boss_director: 'the boss\'s slams, marks and spread shots', hazard: 'enemy hazards (traps, mines, bomb circles)', other: 'unattributed (barrels and the rest)' };
const keys = [...new Set([...Object.keys(gameBy), ...Object.keys(simBy)])].sort((a, b) => ((gameBy[b]?.damage ?? 0) / (gameTotal || 1) + (simBy[b] ?? 0) / (simRaw || 1)) - ((gameBy[a]?.damage ?? 0) / (gameTotal || 1) + (simBy[a] ?? 0) / (simRaw || 1)));
let shown = 0, restG = 0, restS = 0;
for (const k of keys) {
  const gs = gameBy[k] ? gameBy[k].damage / (gameTotal || 1) : 0, ss = simBy[k] ? simBy[k] / (simRaw || 1) : 0;
  if (gs < 0.005 && ss < 0.005) continue;
  if (shown++ >= 14) { restG += gs; restS += ss; continue; }
  console.log(`    ${pad(k, 22)} ${lpad(f(gs * 100), 3)}% | ${lpad(f(ss * 100), 3)}%${gameBy[k] ? `   (${gameBy[k].hits} hits)` : ''}${notModelled[k] ? '   not in the model: ' + notModelled[k] : ''}`);
}

if (restG + restS > 0) console.log(`    ${pad('(the rest)', 22)} ${lpad(f(restG * 100), 3)}% | ${lpad(f(restS * 100), 3)}%`);

// ---- end, chests, points of interest, heals ----
const died = full.filter(r => r.died);
console.log(`\nEnd: game ${H.endReason} at ${clock(end)} | model sinks by then in ${f(full.filter(r => r.died && r.end * 60 <= end).length / full.length * 100)}% of runs; median end ${clock(med(full.map(r => r.end * 60)))}` +
  ` (p10 ${clock(q(full.map(r => r.end * 60), 0.1))}), ${f((full.length - died.length) / full.length * 100)}% afloat at ${MODEL.maxMinutes}:00, boss beaten in ${f(full.filter(r => r.bossKill != null).length / full.length * 100)}%`);
const chests = events.filter(e => e.type === 'chest').length;
console.log(`Chests opened: game ${chests} | model ${range(cut.map(r => r.items.length))} (a chest every ${MODEL.chestEverySeconds}s x0.8 / lootShare)`);
const pois = {}; for (const e of events) if (e.type === 'poi' && e.id !== 'SalvageChest') pois[e.id] = (pois[e.id] ?? 0) + 1;
const landmarks = events.filter(e => e.type === 'landmark').length;
if (Object.keys(pois).length || landmarks) console.log(`Points of interest used in the game: ${Object.entries(pois).map(([k, v]) => `${k} x${v}`).join(', ') || '-'}${landmarks ? `, landmarks x${landmarks}` : ''}. Not in the model (only chests, as a timer).`);
if (session.heals) console.log(`Heals in the game: ${f(session.heals.total)} hull (${f(session.heals.pickupsAndRepairs)} pickups and repairs, ${f(session.heals.regen)} regen). The model heals on a timer (pickups) and does not report totals.`);
if (H.bossSummoned >= 0) console.log(`Boss: summoned at the Beacon ${clock(H.bossSummoned)}; the model summons it then too.`);
const picks = events.filter(e => e.type === 'pick');
if (picks.length) {
  const kinds = {}; for (const p of picks) kinds[p.kind] = (kinds[p.kind] ?? 0) + 1;
  const mix = MODEL.cardMix;
  console.log(`Level-up picks in the game: ${Object.entries(kinds).map(([k, v]) => `${k} ${f(v / picks.length * 100)}%`).join(', ')} | model card mix: upgrade ${mix.upgrade * 100}%, new weapon ${mix.newWeapon * 100}%, Logbook ${mix.logbook * 100}%`);
}
const weapons = session.weapons || {};
const wTotal = Object.values(weapons).reduce((s, w) => s + w.damage, 0);
if (wTotal > 0) console.log(`Weapons in the game: ${Object.entries(weapons).sort((a, b) => b[1].damage - a[1].damage).map(([k, w]) => `${k} ${f(w.damage / wTotal * 100)}% (${w.kills} kills)`).join(', ')}`);

// ---- mismatches and the constants that move them ----
// Metrics at the session's last full minute (or its end), each with the game value and the model's runs.
const metrics = [];
const at = perMinute[perMinute.length - 1];
if (at && at.g) {
  metrics.push({ name: `kills at ${at.m}:00`, key: 'kills', g: at.g.kills, sim: at.kills });
  metrics.push({ name: `level at ${at.m}:00`, key: 'level', g: at.g.level, sim: at.level });
  // Like with like: the game's damage from sources the model has (no island, storm, hazard or barrel damage).
  metrics.push({ name: `enemy damage to ${at.m}:00`, key: 'dmg', g: at.g.damageTaken * modelledShare, sim: at.dmg });
}
const mid = perMinute[Math.floor((perMinute.length - 1) / 2)];
if (mid && mid !== at && mid.g) metrics.push({ name: `kills at ${mid.m}:00`, key: 'killsMid', g: mid.g.kills, sim: mid.kills });
for (const r of fightRows) if (r.g != null && r.sim.length) metrics.push({ name: `${r.kind} kill time`, key: 'fight:' + r.kind, g: r.g, sim: r.sim });
const lv5 = lvRows.find(r => r.l === 5); if (lv5 && lv5.g != null) metrics.push({ name: 'time to level 5', key: 'lv5', g: lv5.g, sim: lv5.sim });
if (H.endReason === 'sunk') metrics.push({ name: 'time sunk', key: 'end', g: end, sim: full.map(r => r.end * 60) });
for (const m of metrics) {
  // Spread floor: 5% of the median (or 0.5), so a model with no spread does not rank every tiny gap first.
  const s = sorted(m.sim), md = q(s, 0.5), spread = Math.max((q(s, 0.9) - q(s, 0.1)) / 2.56, Math.abs(md) * 0.05, 0.5);
  m.med = md; m.z = (m.g - md) / spread; m.rel = md ? (m.g - md) / Math.abs(md) : 0;
}
metrics.sort((a, b) => Math.abs(b.z) - Math.abs(a.z));
const close = metrics.filter(m => Math.abs(m.z) < 1 || Math.abs(m.rel) < 0.05);

// Sensitivity: each constant +30% on a smaller batch, the change in each metric's median.
const metricOf = (runs, key) => {
  const mm = at ? at.m : 1;
  if (key === 'kills') return med(runs.filter(r => r.minutes.some(x => x.m === mm)).map(r => simCumKills(r, mm)));
  if (key === 'killsMid') return med(runs.filter(r => r.minutes.some(x => x.m === mid.m)).map(r => simCumKills(r, mid.m)));
  if (key === 'level') return med(runs.map(r => (r.minutes.find(x => x.m === mm) || {}).level));
  if (key === 'dmg') return med(runs.filter(r => r.minutes.some(x => x.m === mm)).map(r => simCumDmg(r, mm)));
  if (key === 'lv5') return med(runs.map(r => r.levelAt[3]));
  if (key === 'end') return med(runs.map(r => r.end * 60));
  if (key.startsWith('fight:')) { const k = key.slice(6); return med(runs.flatMap(r => r.focus.filter(x => simKind(x[0], x[1]) === k).map(x => x[2]))); }
  return null;
};
const knobs = ['accuracy', 'engagement', 'lootShare', 'topUpPerSecond', 'arrivalSeconds', 'contactRate', 'dodge', 'shotHitChance', 'focusHitsPerSecond', 'bossHitsPerSecond', 'focusShareOfDps', 'eliteXp', 'chestEverySeconds'];
const hints = {
  kills: 'kill throughput: accuracy, engagement, topUpPerSecond (kills are spawn-limited), arrivalSeconds',
  killsMid: 'kill throughput: accuracy, engagement, topUpPerSecond, arrivalSeconds',
  level: 'XP: lootShare, accuracy (kills), eliteXp; data: Pickups xpPickupMultiplier, the XP table',
  dmg: 'incoming damage: contactRate, dodge (melee), shotHitChance, shotInterval (ranged), focusHitsPerSecond, bossHitsPerSecond',
  lv5: 'early XP: lootShare, accuracy, arrivalSeconds',
  end: 'survival: contactRate, shotHitChance, focusHitsPerSecond, bossHitsPerSecond, dodge; and the kill side (accuracy)',
};
const n = Math.max(40, Math.round(opt.seeds / 4));
const baseSmall = batch({}, n, MODEL.maxMinutes);
const effects = {};
for (const k of knobs) {
  const v = (opt.set[k] ?? MODEL[k]) * 1.3;
  const runs = batch({ [k]: k === 'dodge' ? Math.min(0.95, v) : k === 'focusShareOfDps' || k === 'engagement' || k === 'lootShare' || k === 'accuracy' ? Math.min(1, v) : v }, n, MODEL.maxMinutes);
  effects[k] = {}; for (const m of metrics) { const a = metricOf(baseSmall, m.key), b = metricOf(runs, m.key); effects[k][m.key] = a != null && b != null ? b - a : 0; }
}
console.log('\nBiggest mismatches (game vs model median; z = gap in model spreads) and the constants that close them');
for (const m of metrics.filter(x => !close.includes(x)).slice(0, 6)) {
  const gap = m.g - m.med;
  console.log(`  ${pad(m.name, 26)} game ${f(m.g, 1)} | model ${f(m.med, 1)}  (${m.rel >= 0 ? '+' : ''}${f(m.rel * 100)}%, z ${f(m.z, 1)})`);
  const movers = knobs.map(k => ({ k, e: effects[k][m.key] })).filter(x => Math.abs(x.e) > 1e-6).sort((a, b) => Math.abs(b.e) - Math.abs(a.e)).slice(0, 4);
  if (movers.length) console.log(`      ${movers.map(x => { const dir = Math.sign(x.e) === Math.sign(gap) ? 'raise' : 'lower'; const steps = Math.abs(gap / x.e); return `${dir} ${x.k} (+30% moves it ${x.e >= 0 ? '+' : ''}${f(x.e, 1)}${steps < 5 ? `, ~${f(steps * 30)}% to close` : ''})`; }).join('; ')}`);
  const base = m.key.startsWith('fight:') ? 'fight length: focusShareOfDps, accuracy (min 0.6 on a focus target); data: SaltwindDirector time-to-kill floors (ttk)' : hints[m.key];
  if (base) console.log(`      levers: ${base}`);
}

if (close.length) console.log(`  close enough (within the model's spread or 5%): ${close.map(m => `${m.name} ${f(m.g, 1)} | ${f(m.med, 1)}`).join('; ')}`);

// ---- what the model leaves out ----
const missing = [];
if (Object.keys(pois).length || landmarks) missing.push('points of interest other than chests (merchant skiff, trial buoy, repair wrecks, landmarks)');
if (Object.keys(gameBy).some(k => notModelled[k] && k !== 'boss_director')) missing.push('island, storm, hazard and barrel damage (game sources: ' + Object.keys(gameBy).filter(k => notModelled[k] && k !== 'boss_director').join(', ') + ')');
if (gameBy.boss_director) missing.push('the boss director\'s slams and spread shots (the model has one boss hit rate: bossHitsPerSecond)');
missing.push('the walk to the Beacon (the boss time comes from the session), movement and dodging (one dodge constant), pickups out of reach (lootShare)');
console.log(`\nNot in the model: ${missing.join('; ')}.`);
