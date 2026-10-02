// Batch runs for the report: every captain, many seeds; a grid over spawn density and enemy damage (same for every
// enemy, so an enemy that looks the same always has the same stats); per weapon, Logbook, item and enemy statistics.
import fs from 'fs';
import { simulateRun, summarise } from './sim.mjs';
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const captains = Object.keys(data.captains);
const runsPer = +(process.argv[2] || 60);
const t0 = Date.now();
function batch(params, n = runsPer) { const runs = []; let seed = 1; for (const c of captains) for (let i = 0; i < n; i++) runs.push(simulateRun(data, { ...params, captain: c, seed: seed++ * 7919 })); return runs; }
const base = batch({});
const out = { generated: new Date().toISOString(), runsPerCaptain: runsPer, base: summarise(base), captains: {}, grid: [] };
for (const c of captains) out.captains[c] = summarise(base.filter(r => r.captain === c));
console.log('base', JSON.stringify(out.base, (k, v) => k === 'byMinute' ? undefined : v), ((Date.now() - t0) / 1000).toFixed(1) + 's');
fs.writeFileSync(new URL('./base-sample.json', import.meta.url), JSON.stringify(base.slice(0, 3), null, 1));
