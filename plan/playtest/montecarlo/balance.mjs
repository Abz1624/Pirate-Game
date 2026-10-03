// Weapon roles (owner, 3 Oct): every weapon should carry a voyage about equally. Each weapon sails alone (no second
// weapon; upgrades and Logbooks as usual) and its base damage is scaled until its median voyage matches the field's.
// Single-target specialists gain damage, strong crowd weapons give some back. Prints the scale per weapon.
import fs from 'fs';
import { simulateRun } from './sim.mjs';
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const ids = Object.keys(data.weapons), N = 40;
const scale = Object.fromEntries(ids.map(id => [id, 1]));
const median = a => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };
const carry = (id) => { const d = JSON.parse(JSON.stringify(data)); d.weapons[id].rateScale = scale[id]; const e = [];
  for (let i = 0; i < N; i++) e.push(simulateRun(d, { captain: 'brine', startWeapon: id, solo: true, seed: 31 + i * 17, model: { lootShare: 0.8, engagement: 0.9 } }).end);
  return median(e); };
let ends = {};
for (let round = 0; round < 1; round++) {
  for (const id of ids) ends[id] = carry(id);
  const target = median(Object.values(ends));
  const spread = Math.max(...Object.values(ends)) - Math.min(...Object.values(ends));
  console.log(`round ${round}: target ${target.toFixed(1)} min, spread ${spread.toFixed(1)} min`);
  if (spread < 1.5) break;
  for (const id of ids) scale[id] = Math.min(3, Math.max(0.4, scale[id] * Math.pow(target / Math.max(0.5, ends[id]), 0.9)));
}
const out = ids.map(id => ({ id, name: data.weapons[id].name, scale: +scale[id].toFixed(3), end: +ends[id].toFixed(1), cooldownFrom: data.weapons[id].cooldown, cooldownTo: +(data.weapons[id].cooldown / scale[id]).toFixed(3) }));
out.sort((a, b) => b.scale - a.scale); console.table(out);
fs.writeFileSync(new URL('./weapon-balance.json', import.meta.url), JSON.stringify(out, null, 1));
