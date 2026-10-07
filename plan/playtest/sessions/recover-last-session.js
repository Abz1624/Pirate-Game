// Recovers the last finished voyage's session file from the WebGL build's saved data (PlayerPrefs "rp.session.last").
// Open the game's page (e.g. http://localhost:8090), press F12, open the Console tab, paste this whole file, press Enter.
// It downloads session-last.json. Only the most recent finished voyage is kept; a newer voyage overwrites it.
(async () => {
  const db = await new Promise((ok, fail) => { const r = indexedDB.open('/idbfs'); r.onsuccess = () => ok(r.result); r.onerror = fail; });
  const store = () => db.transaction('FILE_DATA', 'readonly').objectStore('FILE_DATA');
  const keys = await new Promise(ok => { const r = store().getAllKeys(); r.onsuccess = () => ok(r.result); });
  const values = await new Promise(ok => { const r = store().getAll(); r.onsuccess = () => ok(r.result); });
  for (let i = 0; i < keys.length; i++) {
    if (!String(keys[i]).endsWith('PlayerPrefs') || !values[i] || !values[i].contents) continue;
    const text = new TextDecoder('utf-8').decode(values[i].contents);
    const start = text.lastIndexOf('{"format":"rp-session-1"');
    if (start < 0) continue;
    let depth = 0, end = start, inString = false;
    for (; end < text.length; end++) {
      const c = text[end];
      if (inString) { if (c === '\\') end++; else if (c === '"') inString = false; continue; }
      if (c === '"') inString = true;
      else if (c === '{') depth++;
      else if (c === '}' && --depth === 0) break;
    }
    const json = text.slice(start, end + 1);
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
    a.download = 'session-last.json';
    a.click();
    return 'Downloaded session-last.json (' + json.length + ' characters)';
  }
  return 'No saved session found in this browser for this page';
})();
