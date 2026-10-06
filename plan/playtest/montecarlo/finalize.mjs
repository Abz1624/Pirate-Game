// Adds the applied game data's results and the weapon table to results.json (for the report).
import fs from 'fs';
import { simulateRun, summarise, MODEL } from './sim.mjs';
const out = JSON.parse(fs.readFileSync(new URL('./results.json', import.meta.url)));
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const runs = []; let seed = 1;
for (const c of Object.keys(data.captains)) for (let i = 0; i < 150; i++) runs.push(simulateRun(data, { captain: c, seed: (seed++) * 7919 + 17, model: { lootShare: 0.8, engagement: 0.9 } }));
out.applied = summarise(runs);
out.appliedCaptains = {};
for (const c of Object.keys(data.captains)) { const s = summarise(runs.filter(r => r.captain === c)); s.weapon = data.captains[c].weapon; out.appliedCaptains[c] = s; }
const stat = (has) => { const a = runs.filter(has); const s = a.map(r => r.end).sort((x, y) => x - y); return { runs: a.length, end: s.length ? s[Math.floor(s.length / 2)] : null, bossWin: a.length ? a.filter(r => r.bossKill != null).length / a.length : null }; };
out.weapons = Object.keys(data.weapons).map(id => ({ id, ...stat(r => r.weapons.includes(id)), evolved: runs.filter(r => r.evolved.includes(id)).length }));
out.logbooks = Object.keys(data.logbooks).map(id => ({ id, ...stat(r => r.logbooks.includes(id)) }));
out.items = Object.keys(data.items).map(id => ({ id, ...stat(r => r.items.includes(id)) }));
const dmg = {}, kills = {}; for (const r of runs) { for (const [k, v] of Object.entries(r.damageBy)) dmg[k] = (dmg[k] ?? 0) + v; for (const [k, v] of Object.entries(r.killsBy)) kills[k] = (kills[k] ?? 0) + v; }
const total = Object.values(dmg).reduce((a, b) => a + b, 0);
out.enemies = Object.keys(dmg).map(id => ({ id, damageShare: dmg[id] / total, killsPerRun: (kills[id] ?? 0) / runs.length })).sort((a, b) => b.damageShare - a.damageShare);
const focus = {}; for (const r of runs) for (const [id, , secs] of r.focus) (focus[id.startsWith('elite_') && !id.startsWith('elite_hammerhead') && !id.startsWith('elite_kraken') ? 'kill-band elites' : id] ??= []).push(secs);
out.focusFights = Object.entries(focus).map(([id, a]) => ({ id, fights: a.length, medianSeconds: a.sort((x, y) => x - y)[Math.floor(a.length / 2)] }));
// d. Weapon table: level 1, single target, the model's per-weapon behaviour numbers.
out.weaponTable = Object.values(data.weapons).map(w => { const m = MODEL.weapons[w.id]; const lvl = 1.1; return { id: w.id, name: w.name, baseDamage: w.damage, perHit: +(w.damage * m[2] * lvl).toFixed(2), cycle: m[1], hitsPerCycle: m[0], dps: +(w.damage * m[0] * lvl / m[1]).toFixed(2), crowdTargets: m[3], crowdDps: +(w.damage * m[0] * lvl / m[1] * m[3]).toFixed(1) }; }).sort((a, b) => a.name.localeCompare(b.name));
out.appliedChange = { ttk: data.director.ttk, bands: data.timeline.bands.map(b => [b.t, b.rate]) };
fs.writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify(out));
console.log('applied', out.applied.endMedian.toFixed(1), out.applied.bossWin.toFixed(2), 'weapons', out.weaponTable.length, 'focus', JSON.stringify(out.focusFights));
