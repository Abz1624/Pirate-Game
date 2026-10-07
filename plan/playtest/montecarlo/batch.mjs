// The report's numbers (results.json): the game as it is now (data.json, the Unity export) with the model fitted on the
// owner's recorded voyages, every Captain; a grid over the fight-length floors, spawn density and enemy damage; per
// weapon / Logbook / item / enemy stats. V5 M8z (I8z.7): the round-5 "played" data and its comparison are retired; the
// calibration evidence is the recorded sessions (finalize.mjs adds it).
//
//   node batch.mjs [runsPerCaptain=100]   then   node finalize.mjs   then   python build_report.py
import fs from 'fs';
import { simulateRun, summarise, MODEL } from './sim.mjs';
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const captains = Object.keys(data.captains);
const N = +(process.argv[2] || 100), t0 = Date.now();
const batch = (d, params, n = N) => { const runs = []; let seed = 1; for (const c of captains) for (let i = 0; i < n; i++) runs.push(simulateRun(d, { ...params, captain: c, seed: (seed++) * 7919 + 17 })); return runs; };
const fun = s => { // 100 = a voyage that beats the boss and sinks in overtime (after 15:00), with real danger on the way
  const end = s.endMedian, p = [];
  p.push(Math.min(40, Math.abs(end - 16.5) * 8));
  p.push(Math.min(25, Math.max(0, 0.55 - s.bossWin) * 60 + Math.max(0, s.bossWin - 0.85) * 60));
  p.push(Math.min(20, Math.max(0, 0.25 - s.tension) * 60 + Math.max(0, s.tension - 0.55) * 40));
  p.push(Math.min(15, Math.max(0, s.deathBefore10 - 0.1) * 60));
  return Math.max(0, Math.round(100 - p.reduce((a, b) => a + b, 0)));
};
const out = { generated: new Date().toISOString(), runsPerCaptain: N, model: MODEL, meta: data.meta || null, captains: {}, names: {} };
for (const [k, v] of Object.entries({ ...data.weapons, ...data.logbooks, ...data.items, ...data.captains, ...data.enemies })) out.names[k] = v.name;
const base = batch(data, {});
out.current = summarise(base); out.current.fun = fun(out.current);
out.applied = out.current; // the template's "now in the game" card
out.appliedCaptains = {};
for (const c of captains) { const s = summarise(base.filter(r => r.captain === c)); s.weapon = data.captains[c].weapon; s.fun = fun(s); out.captains[c] = s; out.appliedCaptains[c] = s; }
// Grid: fight-length floor scale (on the theoretical DPS), early density (before 5:00), late density, enemy damage.
out.grid = [];
const withTtk = k => { const d = JSON.parse(JSON.stringify(data)); for (const key of Object.keys(d.director.ttk)) d.director.ttk[key] *= k; return d; };
for (const ttkScale of [0.5, 1, 2]) { const d = withTtk(ttkScale);
  for (const earlyMult of [0.8, 1, 1.25]) for (const lateMult of [0.8, 1, 1.25]) for (const enemyDamageMult of [0.75, 1, 1.5]) {
    const s = summarise(batch(d, { earlyMult, lateMult, enemyDamageMult }, Math.max(12, Math.round(N / 5))));
    out.grid.push({ ttkScale, earlyMult, lateMult, enemyDamageMult, end: s.endMedian, bossWin: s.bossWin, tension: s.tension, deathBefore10: s.deathBefore10, survive20: s.survive20, fun: fun(s) });
  } }
out.best = [...out.grid].sort((a, b) => b.fun - a.fun || Math.abs(a.end - 16.5) - Math.abs(b.end - 16.5))[0];
// What builds and enemies do (the game as it is now): per weapon / Logbook / item presence, and enemy damage shares.
const stat = has => { const a = base.filter(has); const s = a.map(r => r.end).sort((x, y) => x - y); return { runs: a.length, end: s.length ? s[Math.floor(s.length / 2)] : null, bossWin: a.length ? a.filter(r => r.bossKill != null).length / a.length : null }; };
out.weapons = Object.keys(data.weapons).map(id => ({ id, ...stat(r => r.weapons.includes(id)), evolved: base.filter(r => r.evolved.includes(id)).length }));
out.logbooks = Object.keys(data.logbooks).map(id => ({ id, ...stat(r => r.logbooks.includes(id)) }));
out.items = Object.keys(data.items).map(id => ({ id, ...stat(r => r.items.includes(id)) }));
const dmg = {}, kills = {}; for (const r of base) { for (const [k, v] of Object.entries(r.damageBy)) dmg[k] = (dmg[k] ?? 0) + v; for (const [k, v] of Object.entries(r.killsBy)) kills[k] = (kills[k] ?? 0) + v; }
const totalDmg = Object.values(dmg).reduce((a, b) => a + b, 0);
out.enemies = Object.keys(dmg).map(id => ({ id, damageShare: dmg[id] / totalDmg, killsPerRun: (kills[id] ?? 0) / base.length })).sort((a, b) => b.damageShare - a.damageShare);
const focus = {}; for (const r of base) for (const [id, , secs] of r.focus) (focus[id.startsWith('adaptive:') ? 'kill-band elites' : id] ??= []).push(secs);
out.focusFights = Object.entries(focus).map(([id, a]) => ({ id, fights: a.length, medianSeconds: a.sort((x, y) => x - y)[Math.floor(a.length / 2)] }));
out.sampleRuns = base.filter((_, i) => i % 50 === 0).slice(0, 12).map(r => ({ captain: r.captain, end: r.end, bossKill: r.bossKill, minutes: r.minutes, weapons: r.weapons, logbooks: r.logbooks, items: r.items }));
// Weapon table: level 1, single target, the model's per-weapon behaviour numbers, and the game's theoretical DPS
// (PlayerPowerIndex: damage x new-weapon bonus x level factor / cooldown x quantity x coverage x hits per firing).
const nb = data.progression.newWeaponDamageBonus ?? 1.1;
out.weaponTable = Object.values(data.weapons).map(w => { const m = MODEL.weapons[w.id] || [1, 1, 1, 1]; const lvl = w.levelDamageFactor === false ? nb : nb * 0.1 + 1;
  return { id: w.id, name: w.name, baseDamage: w.damage, perHit: +(w.damage * m[2] * lvl).toFixed(2), cycle: m[1], hitsPerCycle: m[0], dps: +(w.damage * m[0] * lvl / m[1]).toFixed(2), crowdTargets: m[3], crowdDps: +(w.damage * m[0] * lvl / m[1] * m[3]).toFixed(1),
    gameDps: +(w.damage * lvl / w.cooldown * w.quantity * (w.coverage ?? 1) * (w.hitsPerFiring ?? 1)).toFixed(1) }; }).sort((a, b) => a.name.localeCompare(b.name));
out.seconds = (Date.now() - t0) / 1000;
fs.writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify(out));
console.log('current', JSON.stringify({ end: out.current.endMedian, boss: out.current.bossWin, tension: out.current.tension, d10: out.current.deathBefore10, fun: out.current.fun }));
console.log('captains', Object.entries(out.captains).map(([k, s]) => `${k} ${s.endMedian.toFixed(1)}`).join(', '));
console.log('best', JSON.stringify(out.best), 'in', out.seconds.toFixed(0) + 's');
