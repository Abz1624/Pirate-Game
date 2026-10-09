// CDP driver: opens the game in a real Chrome tab, logs its console with timestamps, hides the tab by opening another
// foreground tab on a schedule, then brings the game back. Usage:
// node cdp-hide.mjs <port> <url> <out.log> <totalSeconds> '[[atS, hideS], ...]'  (atS: seconds after the first "sailing" line)
import { writeFileSync, appendFileSync } from 'node:fs';
const [port, url, out, total, schedJson] = process.argv.slice(2);
const schedule = JSON.parse(schedJson || '[]');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const t0 = Date.now();
const ts = () => ((Date.now() - t0) / 1000).toFixed(2).padStart(8);
writeFileSync(out, '');
const log = (s) => { appendFileSync(out, `${ts()} ${s}\n`); };

const ver = await (await fetch(`http://localhost:${port}/json/version`)).json();
const ws = new WebSocket(ver.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener('open', r));
let id = 0;
const pending = new Map();
const handlers = [];
ws.addEventListener('message', (ev) => {
  const m = JSON.parse(ev.data);
  if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(JSON.stringify(m.error))) : p.res(m.result); return; }
  for (const h of handlers) h(m);
});
const send = (method, params = {}, sessionId) => new Promise((res, rej) => { const i = ++id; pending.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params, sessionId })); });

await send('Storage.clearDataForOrigin', { origin: new URL(url).origin, storageTypes: 'cookies,local_storage,indexeddb,cache_storage,service_workers' }).catch(e => console.error('clear failed', e.message)); // a fresh guest profile for each run (the API's database is per session)
const { targetId: game } = await send('Target.createTarget', { url, background: false });
const { sessionId } = await send('Target.attachToTarget', { targetId: game, flatten: true });
let sailingAt = -1;
handlers.push((m) => {
  if (m.sessionId !== sessionId) return;
  if (m.method === 'Runtime.consoleAPICalled') {
    const text = m.params.args.map((a) => a.value ?? a.description ?? '').join(' ');
    if (!new RegExp(process.env.PATTERN || 'V5 Net|V5 NetClient|Exception|Error|error').test(text)) return;
    log(text.replace(/\n/g, ' | ').slice(0, 4000));
    if (sailingAt < 0 && new RegExp(process.env.START || 'stage built').test(text)) sailingAt = Date.now();
  } else if (m.method === 'Runtime.exceptionThrown') log('EXCEPTION ' + JSON.stringify(m.params.exceptionDetails).slice(0, 1000));
});
if (process.env.EMULATE) { const [w, h] = process.env.EMULATE.split('x').map(Number); await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }, sessionId); }
await send('Runtime.enable', {}, sessionId);
await send('Page.enable', {}, sessionId);
log('opened ' + url);
if (process.env.CANVAS) {
  const [cw, ch] = process.env.CANVAS.split('x');
  const setCanvas = async () => { try { await send('Runtime.evaluate', { expression: `(() => { const c = document.querySelector('#unity-canvas'); if (!c) return 'none'; c.style.width='${cw}px'; c.style.height='${ch}px'; return c.clientWidth + 'x' + c.clientHeight; })()` }, sessionId); } catch (e) {} };
  for (const ms of [1000, 3000, 8000, 15000]) setTimeout(setCanvas, ms);
  setTimeout(async () => { const r = await send('Runtime.evaluate', { expression: `(() => { const c = document.querySelector('#unity-canvas'); return c ? c.clientWidth + 'x' + c.clientHeight + ' drawing ' + c.width + 'x' + c.height : 'none'; })()` }, sessionId); log('canvas ' + r.result.value); }, 20000);
}
const vis = async () => (await send('Runtime.evaluate', { expression: 'document.visibilityState' }, sessionId)).result.value;

const deadline = Date.now() + Number(total) * 1000;
while (sailingAt < 0 && Date.now() < deadline) await sleep(200);
log('sailing seen');
for (const [at, hide] of schedule) {
  while (Date.now() < sailingAt + at * 1000) await sleep(50);
  const { targetId: other } = await send('Target.createTarget', { url: 'about:blank', background: false });
  await send('Target.activateTarget', { targetId: other });
  await sleep(300);
  log(`HIDE for ${hide}s (game ${await vis()})`);
  await sleep(hide * 1000 - 300);
  await send('Target.activateTarget', { targetId: game });
  await send('Page.bringToFront', {}, sessionId);
  await send('Target.closeTarget', { targetId: other });
  await sleep(200);
  log(`SHOW (game ${await vis()})`);
}
while (Date.now() < deadline) await sleep(500);
log('done');
await send('Target.closeTarget', { targetId: game });
process.exit(0);
