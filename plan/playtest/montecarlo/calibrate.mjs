// Fits the model's free constants to the owner's playtest (3 Oct; Captain Rook, Swivel Guns): about 515 kills and
// level 24 at 5:13, 702 kills and level 28 at 7:18. Step 1: accuracy (kill throughput). Step 2 (report): damage
// constants for a given median voyage length.
import fs from 'fs';
import { simulateRun } from './sim.mjs';
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const med = a => { const s = a.filter(x => !isNaN(x)).sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : NaN; };
const rows = [];
for (const accuracy of [0.08, 0.1, 0.12, 0.15, 0.2, 0.25]) {
  const model = { accuracy, contactRate: 0.0001, shotHitChance: 0.0001, focusHitsPerSecond: 0.0001, bossHitsPerSecond: 0.0001 };
  const runs = []; for (let i = 0; i < 40; i++) runs.push(simulateRun(data, { captain: 'rook', seed: 101 + i * 13, model }));
  const cum = (m) => med(runs.map(r => r.minutes.filter(x => x.m <= m).reduce((s, y) => s + y.kills, 0)));
  const lvl = (m) => med(runs.map(r => (r.minutes.find(x => x.m === m) || {}).level));
  rows.push({ accuracy, kills5: cum(5), level5: lvl(5), kills7: cum(7), level7: lvl(7), alive7: med(runs.map(r => (r.minutes.find(x => x.m === 7) || {}).alive)), err: +(Math.abs(cum(5) / 495 - 1) + Math.abs(lvl(5) / 24 - 1) + Math.abs(cum(7) / 680 - 1) + Math.abs(lvl(7) / 28 - 1)).toFixed(3) });
}
console.table(rows);
