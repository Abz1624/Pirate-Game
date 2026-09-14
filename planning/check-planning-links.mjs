import assert from 'node:assert/strict';
import { access, readdir, readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';

const root = process.cwd();
const names = (await readdir(root)).filter(name => /^Rogue_Pirates_.*\.md$/.test(name));
const missing = [];

for (const name of names) {
  const source = await readFile(resolve(root, name), 'utf8');
  for (const match of source.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
    const raw = match[1].trim().replace(/^<|>$/g, '');
    if (/^(?:https?:|mailto:|#)/i.test(raw)) continue;
    const target = decodeURIComponent(raw.split('#')[0]);
    try {
      await access(resolve(dirname(resolve(root, name)), target));
    } catch {
      missing.push(`${name} -> ${raw}`);
    }
  }
}

assert.deepEqual(missing, [], `Missing local Markdown links:\n${missing.join('\n')}`);
console.log(`PASS: local Markdown links resolve across ${names.length} Rogue Pirates planning documents.`);
