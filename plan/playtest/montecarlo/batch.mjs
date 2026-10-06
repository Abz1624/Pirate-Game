// The report's numbers: the owner's profile (80% of the loot, 90% of the enemies engaged) on the current data, every
// captain; a grid over spawn density x enemy damage (the same for every enemy); per weapon / Logbook / item / enemy stats.
import fs from 'fs';
import { simulateRun, summarise, MODEL } from './sim.mjs';
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const played = JSON.parse(fs.readFileSync(new URL('./data-played.json', import.meta.url)));
const captains = Object.keys(data.captains);
const N = +(process.argv[2] || 100), t0 = Date.now();
const profile = { lootShare: 0.8, engagement: 0.9 };
const batch = (d, params, n = N) => { const runs = []; let seed = 1; for (const c of captains) for (let i = 0; i < n; i++) runs.push(simulateRun(d, { ...params, model: { ...profile, ...(params.model || {}) }, captain: c, seed: (seed++) * 7919 + 17 })); return runs; };
const fun = s => { // 100 = a voyage that beats the boss and sinks in overtime, with real danger on the way
  const end = s.endMedian, p = [];
  p.push(Math.min(40, Math.abs(end - 16.5) * 8));
  p.push(Math.min(25, Math.max(0, 0.55 - s.bossWin) * 60 + Math.max(0, s.bossWin - 0.85) * 60));
  p.push(Math.min(20, Math.max(0, 0.25 - s.tension) * 60 + Math.max(0, s.tension - 0.55) * 40));
  p.push(Math.min(15, Math.max(0, s.deathBefore10 - 0.1) * 60));
  return Math.max(0, Math.round(100 - p.reduce((a, b) => a + b, 0)));
};
const out = { generated: new Date().toISOString(), runsPerCaptain: N, model: MODEL, profile, captains: {}, names: {} };
for (const [k, v] of Object.entries({ ...data.weapons, ...data.logbooks, ...data.items, ...data.captains, ...data.enemies })) out.names[k] = v.name;
const playedRuns = batch(played, {}); out.played = summarise(playedRuns); out.played.fun = fun(out.played);
const base = batch(data, {}); out.current = summarise(base); out.current.fun = fun(out.current);
for (const c of captains) { const s = summarise(base.filter(r => r.captain === c)); s.weapon = data.captains[c].weapon; s.fun = fun(s); out.captains[c] = s; }
// Grid: time-to-kill floor scale (on the theoretical DPS), early density (before 5:00), late density, enemy damage
// (the same for every enemy).
out.grid = [];
const withTtk = k => { const d = JSON.parse(JSON.stringify(data)); for (const key of Object.keys(d.director.ttk)) d.director.ttk[key] *= k; return d; };
for (const ttkScale of [0.1, 0.15, 0.2, 0.3]) { const d = withTtk(ttkScale);
  for (const earlyMult of [0.6, 0.8, 1]) for (const lateMult of [0.5, 0.6, 0.75, 0.9]) for (const enemyDamageMult of [0.35, 0.5, 0.65]) {
    const s = summarise(batch(d, { earlyMult, lateMult, enemyDamageMult }, Math.max(12, Math.round(N / 5))));
    out.grid.push({ ttkScale, earlyMult, lateMult, enemyDamageMult, end: s.endMedian, bossWin: s.bossWin, tension: s.tension, deathBefore10: s.deathBefore10, survive20: s.survive20, fun: fun(s) });
  } }
out.best = [...out.grid].sort((a, b) => b.fun - a.fun || Math.abs(a.end - 16.5) - Math.abs(b.end - 16.5))[0];
// The recommended setting in detail.
const { earlyMult, lateMult, enemyDamageMult, ttkScale } = out.best;
const rec = batch(withTtk(ttkScale), { earlyMult, lateMult, enemyDamageMult });
out.recommended = summarise(rec); out.recommended.fun = fun(out.recommended);
// What builds and enemies do (recommended setting): per weapon / Logbook / item presence, and enemy damage shares.
const stat = (runs, has) => { const a = runs.filter(has); const s = a.map(r => r.end).sort((x, y) => x - y); return { runs: a.length, end: s.length ? s[Math.floor(s.length / 2)] : null, bossWin: a.length ? a.filter(r => r.bossKill != null).length / a.length : null }; };
out.weapons = Object.keys(data.weapons).map(id => ({ id, ...stat(rec, r => r.weapons.includes(id)), evolved: rec.filter(r => r.evolved.includes(id)).length }));
out.logbooks = Object.keys(data.logbooks).map(id => ({ id, ...stat(rec, r => r.logbooks.includes(id)) }));
out.items = Object.keys(data.items).map(id => ({ id, ...stat(rec, r => r.items.includes(id)) }));
const dmg = {}, kills = {}; for (const r of rec) { for (const [k, v] of Object.entries(r.damageBy)) dmg[k] = (dmg[k] ?? 0) + v; for (const [k, v] of Object.entries(r.killsBy)) kills[k] = (kills[k] ?? 0) + v; }
const totalDmg = Object.values(dmg).reduce((a, b) => a + b, 0);
out.enemies = Object.keys(dmg).map(id => ({ id, damageShare: dmg[id] / totalDmg, killsPerRun: (kills[id] ?? 0) / rec.length })).sort((a, b) => b.damageShare - a.damageShare);
const focus = {}; for (const r of rec) for (const [id, start, secs, hp] of r.focus) { (focus[id] ??= []).push(secs); }
out.focusFights = Object.entries(focus).map(([id, a]) => ({ id, fights: a.length, medianSeconds: a.sort((x, y) => x - y)[Math.floor(a.length / 2)] }));
out.sampleRuns = rec.filter((_, i) => i % 50 === 0).slice(0, 12).map(r => ({ captain: r.captain, end: r.end, bossKill: r.bossKill, minutes: r.minutes, weapons: r.weapons, logbooks: r.logbooks, items: r.items }));
out.seconds = (Date.now() - t0) / 1000;
fs.writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify(out));
console.log('played', JSON.stringify({ end: out.played.endMedian, boss: out.played.bossWin, tension: out.played.tension, fun: out.played.fun }));
console.log('current', JSON.stringify({ end: out.current.endMedian, boss: out.current.bossWin, tension: out.current.tension, d10: out.current.deathBefore10, fun: out.current.fun }));
console.log('best', JSON.stringify(out.best), 'in', out.seconds.toFixed(0) + 's');
