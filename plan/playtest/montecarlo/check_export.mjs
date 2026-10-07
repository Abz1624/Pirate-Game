// V5 M8z (I8z.7, owner 7 Oct 2026): the model reads the game's real configs. data.json is written by the Unity Editor
// (MonteCarloExport: the config set the game runs on, resolved the game's way); export_data.py reads the same assets'
// YAML directly and is kept as the fallback when no Editor is open. This script runs the fallback into a temporary file
// and lists every field where the two disagree, so a drift in either is seen.
//
//   node check_export.mjs                 (python export_data.py -> a temp file, compared with data.json)
//   node check_export.mjs <a.json> <b.json>
//
// Fields only the Editor export has (the game-resolved extras) are counted, not listed one by one; pass --all to list them.
// Exit code 1 when a shared field differs.
import fs from 'fs';
import os from 'os';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2).filter(a => !a.startsWith('--'));
const listAll = process.argv.includes('--all');
let unityPath = args[0] || path.join(here, 'data.json');
let yamlPath = args[1];
if (!yamlPath) {
  yamlPath = path.join(os.tmpdir(), `rp-export-yaml-${process.pid}.json`);
  const r = spawnSync('python', ['export_data.py'], { cwd: here, env: { ...process.env, RP_OUT: yamlPath }, encoding: 'utf8' });
  if (r.status !== 0) { console.log('export_data.py failed:\n' + r.stdout + r.stderr); process.exit(2); }
}
const a = JSON.parse(fs.readFileSync(unityPath, 'utf8'));
const b = JSON.parse(fs.readFileSync(yamlPath, 'utf8'));
if (!a.meta || a.meta.source !== 'unity') console.log(`note: ${path.basename(unityPath)} has no Unity export stamp (meta.source); it may be a YAML export.`);

const IGNORE = new Set(['meta']);
const differ = [], onlyUnity = [], onlyYaml = [];
const close = (x, y) => x === y || (typeof x === 'number' && typeof y === 'number' && Math.abs(x - y) <= 1e-4 * Math.max(1, Math.abs(x), Math.abs(y)));
const show = v => { const s = JSON.stringify(v); return s && s.length > 70 ? s.slice(0, 67) + '...' : s; };
// Lists of {id: ...} rows (the stat sheet) are compared by id, not by position.
const keyed = v => Array.isArray(v) && v.length && v.every(x => x && typeof x === 'object' && !Array.isArray(x) && 'id' in x);
function walk(x, y, p) {
  if (keyed(x) && keyed(y)) { x = Object.fromEntries(x.map(r => [r.id, r])); y = Object.fromEntries(y.map(r => [r.id, r])); }
  const ox = x !== null && typeof x === 'object', oy = y !== null && typeof y === 'object';
  if (!ox || !oy) { if (!close(x, y)) differ.push([p, x, y]); return; }
  if (Array.isArray(x) !== Array.isArray(y)) { differ.push([p, x, y]); return; }
  if (Array.isArray(x) && x.length !== y.length) { differ.push([p + ' (length)', x.length, y.length]); }
  const keys = new Set([...Object.keys(x), ...Object.keys(y)]);
  for (const k of keys) {
    const q = p ? `${p}.${k}` : k;
    if (!p && IGNORE.has(k)) continue;
    if (!(k in y)) { onlyUnity.push(q); continue; }
    if (!(k in x)) { onlyYaml.push(q); continue; }
    if (Array.isArray(x) && Array.isArray(y) && (+k >= x.length || +k >= y.length)) continue;
    walk(x[k], y[k], q);
  }
}
walk(a, b, '');

console.log(`Unity export: ${path.relative(process.cwd(), unityPath)}${a.meta ? ` (config ${String(a.meta.configHash).slice(0, 10)}, ${a.meta.exported}${a.meta.preset ? ', preset ' + a.meta.preset : ''})` : ''}`);
console.log(`YAML export:  export_data.py`);
console.log(`\nShared fields that differ: ${differ.length}`);
for (const [p, x, y] of differ) console.log(`  ${p}: unity ${show(x)} | yaml ${show(y)}`);
// Grouped by field name: per-id maps (enemies, weapons, ...) and per-row lists (timeline.focus, ...) collapse to `*`.
const ROWS = new Set(['enemies', 'weapons', 'logbooks', 'items', 'evolutions', 'captains']);
const groups = list => { const g = {}; for (const p of list) { const s = p.split('.'); const k = ROWS.has(s[0]) && s.length > 2 ? `${s[0]}.*.${s[2]}` : s[0] === 'timeline' && s.length > 3 && /^(bands|focus|pulses)$/.test(s[1]) ? `timeline.${s[1]}.*.${s[3]}` : s.slice(0, 2).join('.'); g[k] = (g[k] ?? 0) + 1; } return g; };
console.log(`\nOnly in the Unity export (resolved by the game; export_data.py does not read them): ${onlyUnity.length} fields`);
if (listAll) for (const p of onlyUnity) console.log('  ' + p);
else console.log('  ' + Object.keys(groups(onlyUnity)).join(', '));
console.log(`\nOnly in the YAML export: ${onlyYaml.length} fields`);
for (const p of onlyYaml) console.log('  ' + p);
if (!args[1]) fs.rmSync(yamlPath, { force: true });
process.exit(differ.length ? 1 : 0);
