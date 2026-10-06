// Fits the model's two free constants to the owner's playtest of 3 Oct (data-played.json = commit eb538609, Captain
// Rook): about 515 kills and level 24 at 5:13, 702 kills and level 28 at 7:18, hull around 70% then, and still afloat
// in overtime (they quit from boredom). accuracy sets kill throughput; harm scales every incoming-damage rate together.
import fs from 'fs';
import { simulateRun, MODEL } from './sim.mjs';
const data = JSON.parse(fs.readFileSync(new URL('./data-played.json', import.meta.url)));
const med = a => { const s = a.filter(x => x != null && !isNaN(x)).sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : NaN; };
const rows = [];
for (const accuracy of [0.3])
  for (const harm of [0.35, 0.5, 0.75, 1, 1.5, 2, 3])
  for (const topUpPerSecond of [0.5])
  for (const lootShare of [1]) {
    const model = { accuracy, topUpPerSecond, lootShare, contactRate: MODEL.contactRate * harm, shotHitChance: MODEL.shotHitChance * harm, focusHitsPerSecond: MODEL.focusHitsPerSecond * harm, bossHitsPerSecond: MODEL.bossHitsPerSecond * harm };
    const runs = []; for (let i = 0; i < 40; i++) runs.push(simulateRun(data, { captain: 'rook', seed: 101 + i * 13, model }));
    const cum = m => med(runs.filter(r => r.minutes.length >= m).map(r => r.minutes.filter(x => x.m <= m).reduce((s, y) => s + y.kills, 0)));
    const at = (m, k) => med(runs.map(r => (r.minutes.find(x => x.m === m) || {})[k]));
    const k5 = cum(5), k7 = cum(7), l5 = at(5, 'level'), l7 = at(7, 'level'), h6 = at(6, 'hull'), s16 = runs.filter(r => r.end >= 16).length / runs.length;
    const err = Math.abs(k5 / 495 - 1) + Math.abs(k7 / 680 - 1) + Math.abs(l5 / 24 - 1) + Math.abs(l7 / 28 - 1) + Math.abs((h6 || 0) - 0.7) + Math.max(0, 0.8 - s16) * 2;
    rows.push({ accuracy, harm, topUpPerSecond, lootShare, k5, l5, k7, l7, hull6: +(h6 || 0).toFixed(2), afloat16: s16, err: +err.toFixed(3) });
  }
rows.sort((a, b) => a.err - b.err);
console.table(rows.slice(0, 8));
fs.writeFileSync(new URL('./calibration.json', import.meta.url), JSON.stringify({ best: rows[0], table: rows }, null, 1));
