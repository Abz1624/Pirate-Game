// Two-point calibration (3 Oct): on the data the owner played first (data-played.json) Captain Rook sails past 16:00
// with ease; on the round-5 data (data.json before this pass) Rook sinks at about 3:30. Fits the shot hit chance and
// the melee contact rate together (kills/levels were fitted before: accuracy 0.3, top-up 0.5, loot 1).
import fs from 'fs';
import { simulateRun } from './sim.mjs';
const old = JSON.parse(fs.readFileSync(new URL('./data-played.json', import.meta.url)));
const cur = JSON.parse(fs.readFileSync(new URL('./data-died.json', import.meta.url)));
const med = a => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };
const ends = (d, model) => { const e = []; for (let i = 0; i < 40; i++) e.push(simulateRun(d, { captain: 'rook', seed: 101 + i * 13, model }).end); return e; };
const rows = [];
for (const shotHitChance of [0.15, 0.3, 0.5, 0.75]) for (const contactRate of [0.045, 0.1, 0.2, 0.35]) for (const focusHitsPerSecond of [0.125, 0.25, 0.5]) {
  const model = { shotHitChance, contactRate, focusHitsPerSecond, bossHitsPerSecond: focusHitsPerSecond * 1.4 };
  const o = ends(old, model), c = ends(cur, model);
  const oldEnd = med(o), curEnd = med(c), oldAfloat16 = o.filter(x => x >= 16).length / o.length;
  const err = Math.abs(curEnd - 3.5) / 3.5 + Math.max(0, 0.75 - oldAfloat16) * 2 + Math.max(0, 16 - oldEnd) / 16;
  rows.push({ shotHitChance, contactRate, focus: focusHitsPerSecond, oldEnd: +oldEnd.toFixed(1), oldAfloat16, curEnd: +curEnd.toFixed(1), err: +err.toFixed(3) });
}
rows.sort((a, b) => a.err - b.err); console.table(rows.slice(0, 8));
fs.writeFileSync(new URL('./calibration2.json', import.meta.url), JSON.stringify(rows, null, 1));
