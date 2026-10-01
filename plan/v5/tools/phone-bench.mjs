// V5 phone benchmark driver (1 Oct 2026). Runs PhoneBench builds on an Android phone over USB, from this PC:
//   node plan/v5/tools/phone-bench.mjs <plan.json> [out.jsonl]
// plan.json: [{ "build": "Bench-A-base", "label": "A-base", "query": "q=mid" }, ...]
// Needs: the phone on USB with USB debugging, Chrome open on it, and plan/v5/tools/serve-webgl.py serving
// "Pirate Game Unity/Builds" on port 8080. The phone reaches the PC's server as its own localhost (adb reverse), which is a
// secure context (Brotli and the wake lock work); Chrome is driven through its DevTools protocol (adb forward). Each
// variant: open a tab, keep the screen awake, collect the "[Bench] {json}" lines, read the renderer's memory (PSS) and the
// battery temperature at the peak, close the tab. Results are appended to out.jsonl (one line per window).
import { execFileSync } from 'node:child_process';
import { readFileSync, appendFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

const ADB = process.env.ADB || 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe';
const plan = JSON.parse(readFileSync(process.argv[2], 'utf8'));
const out = process.argv[3] || 'plan/v5/perf/phone-bench/results.jsonl';
mkdirSync(dirname(out), { recursive: true });

// ANDROID=1: the installed APK (PACKAGE, default com.x2c.roguepirates) started with the bench query as its command line;
// results read from logcat; memory from dumpsys meminfo of the app. "build" in the plan names an APK to install first
// (adb install -r) when it differs from the last one installed.
const ANDROID = process.env.ANDROID === '1';
const PACKAGE = process.env.PACKAGE || 'com.x2c.roguepirates';
const ACTIVITY = process.env.ACTIVITY || 'com.unity3d.player.UnityPlayerGameActivity';
let installed = null;
// DESKTOP=1: a desktop Chrome on this PC (started with --remote-debugging-port=CDP_PORT); no adb, no phone memory or battery.
const DESKTOP = process.env.DESKTOP === '1';
const CDP = 'http://localhost:' + (process.env.CDP_PORT || '9222');
const adb = (...args) => (DESKTOP ? '' : execFileSync(ADB, args, { encoding: 'utf8' }));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function batteryC() {
  if (DESKTOP) return null;
  const m = /temperature: (\d+)/.exec(adb('shell', 'dumpsys', 'battery'));
  return m ? Number(m[1]) / 10 : null;
}

// The Chrome renderer and GPU processes' PSS in MB (the tab's real memory on the phone).
function chromeMemory() {
  if (DESKTOP) return null;
  const text = adb('shell', 'dumpsys', 'meminfo');
  let renderer = 0, gpu = 0, browser = 0;
  for (const line of text.split('\n')) {
    const m = /^\s*([\d,]+)K: (com\.android\.chrome\S*)/.exec(line);
    if (!m) continue;
    const mb = Number(m[1].replace(/,/g, '')) / 1024;
    if (m[2].includes('sandboxed_process')) renderer = Math.max(renderer, mb);
    else if (m[2].includes('privileged_process')) gpu = Math.max(gpu, mb);
    else if (m[2] === 'com.android.chrome') browser = Math.max(browser, mb);
  }
  return { rendererMB: Math.round(renderer), gpuProcessMB: Math.round(gpu), browserMB: Math.round(browser) };
}

async function json(path, method = 'GET') {
  const r = await fetch(CDP + path, { method });
  return r.json();
}

// One call on the browser-level DevTools connection (tab creation and closing).
async function browserCall(method, params) {
  const { webSocketDebuggerUrl } = await json('/json/version');
  const ws = new WebSocket(webSocketDebuggerUrl);
  await new Promise((resolve) => ws.addEventListener('open', resolve, { once: true }));
  const reply = new Promise((resolve, reject) => ws.addEventListener('message', (e) => {
    const m = JSON.parse(e.data);
    if (m.id === 1) m.error ? reject(new Error(m.error.message)) : resolve(m.result);
  }));
  ws.send(JSON.stringify({ id: 1, method, params }));
  try { return await reply; } finally { ws.close(); }
}

// A sleeping phone throttles Chrome to 1 frame a second (run 3, 2 Oct): never measure one; wait for the owner to wake it.
async function waitAwake() {
  if (DESKTOP) return;
  let warned = false;
  while (!/mWakefulness=Awake/.test(adb('shell', 'dumpsys', 'power'))) {
    if (!warned) { console.log('  phone screen is off: waiting for it to be woken and unlocked'); warned = true; }
    await sleep(10000);
  }
}

// The app's total PSS in MB (Android).
function appMemory() {
  const m = /TOTAL PSS:\s+([\d,]+)/.exec(adb('shell', 'dumpsys', 'meminfo', PACKAGE)) || /TOTAL\s+([\d,]+)/.exec(adb('shell', 'dumpsys', 'meminfo', PACKAGE));
  return m ? { appPssMB: Math.round(Number(m[1].replace(/,/g, '')) / 1024) } : null;
}

async function runAndroidVariant(v) {
  await waitAwake();
  if (v.build && v.build !== installed) {
    console.log(`  installing ${v.build}`);
    adb('install', '-r', v.build);
    installed = v.build;
  }
  const query = `bench=${encodeURIComponent(v.label)}&${v.query || ''}`;
  adb('shell', 'am', 'force-stop', PACKAGE);
  adb('logcat', '-c');
  const started = Date.now();
  const temps = { startC: batteryC() };
  adb('shell', 'am', 'start', '-n', `${PACKAGE}/${ACTIVITY}`, '--es', 'unity', `"-v5bench ${query}"`);
  const results = [];
  let failure = null, finished = false, memory = null, seen = 0;
  const short = /(^|&)short=/.test(v.query || '');
  const limit = (short ? 150 : 330) * 1000;
  let peakAt = 0;
  while (!finished && !failure && Date.now() - started < limit) {
    await sleep(3000);
    const log = adb('logcat', '-d', '-s', 'Unity:I', 'Unity:E', 'CRASH:E', 'DEBUG:E', 'AndroidRuntime:E');
    const lines = log.split('\n');
    for (; seen < lines.length; seen++) {
      const text = lines[seen];
      if (/FATAL|SIGSEGV|SIGABRT|Out of memory/i.test(text)) failure = failure || text.slice(0, 300);
      const i = text.indexOf('[Bench] ');
      if (i < 0) continue;
      const body = text.slice(i + 8).trim();
      if (body === 'done') { finished = true; continue; }
      if (!body.startsWith('{')) continue;
      let r;
      try { r = JSON.parse(body); } catch { continue; }
      r.loadWallSeconds = (Date.now() - started) / 1000;
      results.push(r);
      console.log(`  ${r.phase}: ${r.phase === 'info' ? `${r.gpu} ${r.screen} scale ${r.renderScale} ${r.gfx}` : `fps ${r.fps?.toFixed(1)} p50 ${r.p50} p95 ${r.p95} p99 ${r.p99} >33ms ${r.over33?.toFixed(1)}% enemies ${r.enemiesAvg?.toFixed(0)}`}`);
      if (r.phase === 'early') peakAt = Date.now() + (10 + (short ? 10 : 60) / 2) * 1000;
    }
    if (peakAt && !memory && Date.now() >= peakAt) { memory = appMemory(); temps.peakC = batteryC(); }
    // logcat -d returns the whole buffer (cleared at start): "seen" skips the lines already read.
  }
  if (!memory) memory = appMemory();
  temps.endC = batteryC();
  adb('shell', 'am', 'force-stop', PACKAGE);
  const status = failure ? 'failed' : finished ? 'ok' : 'timeout';
  const record = { label: v.label, build: v.build, query: v.query, status, failure, memory, temps, results, platform: 'android', at: new Date().toISOString() };
  appendFileSync(out, JSON.stringify(record) + '\n');
  console.log(`  -> ${status}${failure ? ': ' + failure.slice(0, 120) : ''} | app ${memory?.appPssMB} MB | battery ${temps.startC}->${temps.peakC ?? '?'}->${temps.endC} C`);
  return record;
}

async function runVariant(v) {
  if (ANDROID) return runAndroidVariant(v);
  await waitAwake();
  const short = /(^|&)short=/.test(v.query || '');
  const url = `http://localhost:8080/${v.build}/RoguePirates/index.html?bench=${encodeURIComponent(v.label)}&${v.query || ''}`;
  // Chrome on Android refuses /json/new: create the tab through the browser connection, then attach to it alone.
  const targetId = await browserCall('Target.createTarget', { url: 'about:blank' }).then((r) => r.targetId);
  const tab = { id: targetId };
  const ws = new WebSocket(`${CDP.replace('http', 'ws')}/devtools/page/${targetId}`);
  let id = 0;
  const send = (method, params = {}) => ws.send(JSON.stringify({ id: ++id, method, params }));
  const started = Date.now();
  const temps = { startC: batteryC() };
  const results = [];
  let failure = null, finished = false, peakMemTimer = null, memory = null;

  await new Promise((resolve) => ws.addEventListener('open', resolve, { once: true }));
  send('Runtime.enable');
  send('Page.enable');
  send('Page.navigate', { url });
  const wake = () => send('Runtime.evaluate', { expression: "navigator.wakeLock && navigator.wakeLock.request('screen').then(()=>'ok').catch(e=>String(e))", awaitPromise: true });

  ws.addEventListener('message', (event) => {
    const msg = JSON.parse(event.data);
    if (msg.method === 'Page.loadEventFired') wake();
    if (msg.method !== 'Runtime.consoleAPICalled' && msg.method !== 'Runtime.exceptionThrown') return;
    const text = msg.method === 'Runtime.exceptionThrown'
      ? String(msg.params.exceptionDetails?.exception?.description || msg.params.exceptionDetails?.text)
      : (msg.params.args || []).map((a) => a.value ?? a.description ?? '').join(' ');
    if (/OOM|abort\(|out of memory|memory access out of bounds/i.test(text)) failure = failure || text.slice(0, 300);
    const i = text.indexOf('[Bench] ');
    if (i < 0) return;
    const body = text.slice(i + 8).trim();
    if (body === 'done') { finished = true; return; }
    if (!body.startsWith('{')) return;
    const r = JSON.parse(body);
    r.loadWallSeconds = (Date.now() - started) / 1000;
    results.push(r);
    console.log(`  ${r.phase}: ${r.phase === 'info' ? `${r.gpu} ${r.screen} scale ${r.renderScale} heap ${r.heapMB} MB textures ${r.textures}` : `fps ${r.fps?.toFixed(1)} p50 ${r.p50} p95 ${r.p95} p99 ${r.p99} >33ms ${r.over33?.toFixed(1)}% enemies ${r.enemiesAvg?.toFixed(0)} heap ${r.heapMB}`}`);
    if (r.phase === 'info') wake();
    if (r.phase === 'early') {
      // The peak window starts ~10 s after "early"; sample memory and temperature in its middle.
      const window = short ? 10 : 60;
      peakMemTimer = setTimeout(() => { memory = chromeMemory(); temps.peakC = batteryC(); }, (10 + window / 2) * 1000);
    }
  });

  const limit = (short ? 150 : 330) * 1000;
  while (!finished && !failure && Date.now() - started < limit) await sleep(1000);
  if (!memory && !failure) memory = chromeMemory();
  if (failure && !memory) memory = chromeMemory();
  clearTimeout(peakMemTimer);
  temps.endC = batteryC();
  try { await browserCall('Target.closeTarget', { targetId: tab.id }); } catch { /* already gone */ }
  ws.close();
  const status = failure ? 'failed' : finished ? 'ok' : 'timeout';
  const record = { label: v.label, build: v.build, query: v.query, status, failure, memory, temps, results, at: new Date().toISOString() };
  appendFileSync(out, JSON.stringify(record) + '\n');
  console.log(`  -> ${status}${failure ? ': ' + failure.slice(0, 120) : ''}` + (DESKTOP ? '' : ` | renderer ${memory?.rendererMB} MB, GPU process ${memory?.gpuProcessMB} MB | battery ${temps.startC}->${temps.peakC ?? '?'}->${temps.endC} C`));
  return record;
}

if (!DESKTOP && !ANDROID) {
  adb('reverse', 'tcp:8080', 'tcp:8080');
  adb('forward', 'tcp:9222', 'localabstract:chrome_devtools_remote');
}
if (ANDROID) console.log(`Android app ${PACKAGE}; ${plan.length} variants -> ${out}`);
else {
  const version = await json('/json/version');
  console.log(`Browser: ${version.Browser}; ${plan.length} variants -> ${out}`);
}
for (const v of plan) {
  console.log(`[${new Date().toLocaleTimeString()}] ${v.label} (${v.build}) ${v.query || ''}`);
  try { await runVariant(v); } catch (e) { console.log('  -> error: ' + e.message); appendFileSync(out, JSON.stringify({ label: v.label, status: 'error', error: e.message }) + '\n'); }
  // Heat skews everything (run 1: the same build lost 29% fps over 28 minutes): wait until the battery is back under
  // COOL_C (default 39 C) or 5 minutes have passed, at least coolSeconds (default 20).
  const coolTo = DESKTOP ? 999 : Number(process.env.COOL_C || 39), t0 = Date.now();
  await sleep(v.coolSeconds != null ? v.coolSeconds * 1000 : 20000);
  let c = batteryC();
  while (c != null && c > coolTo && Date.now() - t0 < 300000) { await sleep(15000); c = batteryC(); }
  console.log(`  cooled to ${c} C in ${Math.round((Date.now() - t0) / 1000)} s`);
}
console.log('all done');
