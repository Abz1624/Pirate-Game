// Round 6 search (3 Oct): owner wants long fights (boss 30-45 s, arenas 20-30 s, kill-band elites 10-25 s) and an
// easier game (they sank at 3-4 min). Minibosses/boss hit 0.2x as hard (long fights), then density x damage x elite rate.
import fs from 'fs';
import { simulateRun, summarise } from './sim.mjs';
const base = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const focusDmg = +(process.argv[2] || 0.2);
const d0 = JSON.parse(JSON.stringify(base)); for (const id of ['reef_crab_miniboss', 'barnacle_king', 'elite_hammerhead', 'elite_kraken_spawn']) d0.enemies[id].dmg *= focusDmg;
const fun = s => { const p = [Math.min(40, Math.abs(s.endMedian - 16.5) * 8), Math.min(25, Math.max(0, 0.55 - s.bossWin) * 60 + Math.max(0, s.bossWin - 0.85) * 60),
  Math.min(20, Math.max(0, 0.25 - s.tension) * 60 + Math.max(0, s.tension - 0.55) * 40), Math.min(30, Math.max(0, s.deathBefore10 - 0.03) * 100)]; return Math.max(0, Math.round(100 - p.reduce((a, b) => a + b, 0))); };
const rows = [];
for (const eliteChance of [0.01, 0.02]) for (const earlyMult of [0.45, 0.55, 0.7]) for (const lateMult of [0.35, 0.45, 0.6]) for (const enemyDamageMult of [0.25, 0.35, 0.5]) {
  const d = JSON.parse(JSON.stringify(d0)); d.director.eliteChance = eliteChance;
  const runs = []; let seed = 1; for (const c of Object.keys(d.captains)) for (let i = 0; i < 20; i++) runs.push(simulateRun(d, { captain: c, seed: (seed++) * 7919 + 17, earlyMult, lateMult, enemyDamageMult, model: { lootShare: 0.8, engagement: 0.9 } }));
  const s = summarise(runs); rows.push({ eliteChance, earlyMult, lateMult, enemyDamageMult, end: +s.endMedian.toFixed(1), boss: +s.bossWin.toFixed(2), tension: +s.tension.toFixed(2), d10: +s.deathBefore10.toFixed(2), s20: +s.survive20.toFixed(2), fun: fun(s) });
}
rows.sort((a, b) => b.fun - a.fun); console.table(rows.slice(0, 10));
fs.writeFileSync(new URL('./search6.json', import.meta.url), JSON.stringify({ focusDmg, rows }, null, 1));
