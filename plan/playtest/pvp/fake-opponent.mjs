// Playtest (3 Oct, owner): a scripted 1v1 opponent for testing the Unity client against the local API (Node 22: global
// fetch and WebSocket). It makes a guest profile, joins a lobby by code (or hosts one and prints the code), drafts,
// readies, then plays the relay protocol: ~15 Hz "state" (circling the arena or chasing the other ship, going for
// barrels), a "hit" on the other ship every second, its own hull through the client's PvP rules (Runtime/Online/PvpRules.cs:
// x0.06, 0.5 s invulnerability windows that pool the hits inside them, at most 5% of max hull per hit), barrel pickups,
// the storm, and optionally sinks itself.
//
//   node plan/playtest/pvp/fake-opponent.mjs --join K7PQ2M [--api http://localhost:8080] [--sinkAfter 45] [--hitDamage 20]
//   node plan/playtest/pvp/fake-opponent.mjs --host [--public]          (prints the code; join it from the game)
//   options: --name "Fake Captain"  --chase (sail at the other ship)  --rematch (ask for a rematch after the end)
//            --stay (do not move)  --noHits  --hold x,z (sail to that arena point and stay there: a match to time)

const args = process.argv.slice(2);
const opt = (name, fallback) => {
  const i = args.indexOf("--" + name);
  if (i < 0) return fallback;
  const v = args[i + 1];
  return v === undefined || v.startsWith("--") ? true : v;
};
const API = String(opt("api", "http://localhost:8080")).replace(/\/$/, "");
const NAME = String(opt("name", "Fake Captain"));
const SINK_AFTER = Number(opt("sinkAfter", 0)); // seconds after the start; 0 = never (the match goes to time)
const HIT_DAMAGE = Number(opt("hitDamage", 20));
const CHASE = !!opt("chase", false);
const STAY = !!opt("stay", false);
const NO_HITS = !!opt("noHits", false);
const REMATCH = !!opt("rematch", false);
const HOLD = opt("hold", null) ? String(opt("hold")).split(",").map(Number) : null; // "x,z": sail there and stay (no barrels)

const log = (...a) => console.log(new Date().toISOString().slice(11, 23), ...a);

async function api(method, path, body, token) {
  const res = await fetch(API + path, {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: "Bearer " + token } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${text}`);
  return json;
}

const DRAFT = {
  weapons: ["deck_cannons", "mortar", "chainshot", "swivel_guns", "harpoon_gun", "long_nine"],
  logbooks: ["gunnery_manual", "ironclad", "admiral", "carpenters_notes", "lookouts_glass", "oversized_shot"],
};

const profile = await api("POST", "/api/profiles", { name: NAME });
log("profile", profile.name + profile.tag, profile.playerId);
const token = profile.token;
const me = profile.playerId;

let lobby;
if (opt("join", null)) {
  lobby = await api("POST", "/api/lobbies/join", { code: String(opt("join")) }, token);
  log("joined lobby", lobby.code, lobby.lobbyId);
} else {
  lobby = await api("POST", "/api/lobbies", { public: !!opt("public", false) }, token);
  log("HOSTED lobby code", lobby.code, "invite", lobby.inviteUrl);
}

const wsUrl = API.replace(/^http/, "ws") + "/ws/lobbies/" + lobby.lobbyId + "?token=" + encodeURIComponent(token);
let ws = connect(wsUrl);

// ---- the match ----
let match = null;
let mySeat = 0, oppId = null;
const ship = { x: 0, z: 0, yaw: 0, vx: 0, vz: 0, hp: 100, maxHp: 100, level: 1, alive: true };
let lastHitAt = -10;
let opp = null; // last state of the other ship
const barrels = new Map();
let outsideFor = 0;
let ended = false;
let startedLogged = false;
let readied = false;
let dealt = 0, taken = 0;
let offset = 0; // server - local clock
const now = () => Date.now() + offset;

function send(o) { if (ws.readyState === 1) ws.send(JSON.stringify(o)); }

function connect(url) {
  const s = new WebSocket(url);
  s.onopen = () => {
    log("socket open");
    if (!readied) {
      readied = true;
      setTimeout(async () => {
        await api("PUT", `/api/lobbies/${lobby.lobbyId}/status`, { status: "drafting" }, token);
        log("status drafting (picking a build for 2 s)");
        setTimeout(async () => {
          await api("PUT", `/api/lobbies/${lobby.lobbyId}/status`, { status: "ready", draft: DRAFT }, token);
          log("status ready with draft");
        }, 2000);
      }, 500);
    }
  };
  s.onmessage = (e) => onMessage(String(e.data));
  s.onclose = (e) => { log("socket closed", e.code, e.reason); if (!ended) setTimeout(() => { ws = connect(url); }, 1500); };
  s.onerror = () => {};
  return s;
}

function onMessage(text) {
  let m;
  try { m = JSON.parse(text); } catch { return; }
  switch (m.t) {
    case "lobby": {
      const seats = (m.seats || []).map((s) => `${s.name}${s.tag}:${s.status}`).join("  ");
      log("lobby", m.status, seats);
      lobby = m;
      break;
    }
    case "match_start": {
      match = m;
      mySeat = m.you;
      offset = m.serverNow - Date.now();
      const opponent = m.players.find((p) => p.seat !== mySeat);
      oppId = opponent ? opponent.playerId : null;
      const sp = m.spawns.find((s) => s.seat === mySeat);
      if (!startedLogged) { ship.x = sp.x; ship.z = sp.z; ship.yaw = sp.yaw; }
      startedLogged = true;
      log(`match_start seat ${mySeat} vs ${opponent ? opponent.name + opponent.tag : "?"}, starts in ${m.startsAt - now()} ms`);
      for (const b of (m.live && m.live.barrels) || []) barrels.set(b.id, b);
      break;
    }
    case "state":
      if (m.from === oppId) opp = m;
      break;
    case "hit":
      if (m.to === me) takeHit(m.dmg, m.weaponId);
      break;
    case "barrel_spawn": barrels.set(m.id, m); log("barrel", m.kind, m.id, `(${m.x.toFixed(1)}, ${m.z.toFixed(1)})`); break;
    case "barrel_gone": barrels.delete(m.id); break;
    case "barrel_taken":
      barrels.delete(m.id);
      log("barrel_taken", m.kind, m.id, m.by === me ? "by ME" : "by opponent");
      if (m.by === me) {
        if (m.kind === "xp") { ship.level++; ship.maxHp += 3; ship.hp += 3; }
        else ship.hp = Math.min(ship.maxHp, ship.hp + ship.maxHp * 0.2);
      }
      break;
    case "presence": log("presence", m.playerId === me ? "me" : "opponent", m.connected ? "connected" : "away, forfeits at " + m.forfeitAt); break;
    case "reject": if (m.of !== "pickup") log("reject", m.of, m.reason); break;
    case "pong": break;
    case "match_end": {
      ended = true;
      const mine = m.stats[me] || {}, theirs = (oppId && m.stats[oppId]) || {};
      log(`MATCH_END winner=${m.winnerPlayerId === me ? "ME (fake)" : m.winnerPlayerId ? "opponent" : "none"} reason=${m.reason} rated=${m.rated} duration=${m.durationSeconds}s`);
      log(`  me: dealt ${mine.damageDealt} taken ${mine.damageTaken} level ${mine.level} barrels ${mine.barrelsTaken} hp ${mine.finalHp}`);
      log(`  them: dealt ${theirs.damageDealt} taken ${theirs.damageTaken} level ${theirs.level} barrels ${theirs.barrelsTaken} hp ${theirs.finalHp}`);
      log("  ratings", JSON.stringify(m.ratings));
      if (REMATCH) { log("asking for a rematch"); send({ t: "rematch" }); }
      else setTimeout(() => process.exit(0), 1500);
      break;
    }
    case "rematch":
      log("rematch", m.status, m.lobbyId || m.by || "");
      if (m.status === "created") setTimeout(() => process.exit(0), 1500);
      break;
    default:
      break;
  }
}

// The client's PvP rules (Runtime/Online/PvpRules.cs, PvpHitWindow): a hit outside the 0.5 s window lands at once (x0.06,
// at most 5% of max hull) and opens the window; hits inside it are pooled and land as one capped hit when it ends.
let windowEnds = 0, pending = 0;
function takeHit(dmg, weaponId) {
  if (!ship.alive) return;
  const t = Date.now() / 1000;
  if (t < windowEnds) { pending += dmg * 0.06; return; }
  windowEnds = t + 0.5;
  applyHull(Math.min(dmg * 0.06, ship.maxHp * 0.05), weaponId + " raw " + dmg);
}

function tickPooled() {
  const t = Date.now() / 1000;
  if (pending <= 0 || t < windowEnds || !ship.alive) return;
  const amount = Math.min(pending, ship.maxHp * 0.05);
  pending = 0;
  windowEnds = t + 0.5;
  applyHull(amount, "pooled hits");
}

function applyHull(amount, what) {
  ship.hp = Math.max(0, ship.hp - amount);
  taken += amount;
  log(`hit (${what}) -> -${amount.toFixed(1)} hull (${ship.hp.toFixed(1)}/${ship.maxHp})`);
  if (ship.hp <= 0) sink("hull at 0");
}

function sink(why) {
  if (!ship.alive) return;
  ship.alive = false;
  ship.hp = 0;
  log("SINKING:", why);
  send(stateMsg());
  send({ t: "dead" });
}

const stateMsg = () => ({
  t: "state", x: +ship.x.toFixed(2), z: +ship.z.toFixed(2), yaw: +ship.yaw.toFixed(1), vx: +ship.vx.toFixed(2), vz: +ship.vz.toFixed(2),
  hp: +ship.hp.toFixed(1), maxHp: ship.maxHp, level: ship.level, alive: ship.alive, boosting: false,
});

function stormRadius(t) {
  const s = match.storm;
  if (t <= s.shrinkStartMs) return s.startRadius;
  if (t >= s.shrinkEndMs) return s.endRadius;
  return s.startRadius + (s.endRadius - s.startRadius) * (t - s.shrinkStartMs) / (s.shrinkEndMs - s.shrinkStartMs);
}

let lastTick = Date.now();
let hitTimer = 0, stateTimer = 0, pingTimer = 0;
setInterval(() => {
  const t = Date.now();
  const dt = (t - lastTick) / 1000;
  lastTick = t;
  pingTimer -= dt;
  if (pingTimer <= 0) { pingTimer = 2; send({ t: "ping", cs: Date.now() }); }
  if (!match || ended) return;
  tickPooled();
  const started = now() >= match.startsAt;
  if (started && ship.alive) {
    const elapsed = (now() - match.startsAt) / 1000;
    if (SINK_AFTER > 0 && elapsed >= SINK_AFTER) sink(`--sinkAfter ${SINK_AFTER}`);
    // where to go: a barrel within 50 m, else the other ship (chase) or a circle around the centre
    let gx, gz;
    let best = null, bestD = 50;
    for (const b of barrels.values()) {
      const d = Math.hypot(b.x - ship.x, b.z - ship.z);
      if (d < bestD) { bestD = d; best = b; }
    }
    const radius = stormRadius(now());
    if (HOLD) { gx = HOLD[0]; gz = HOLD[1]; }
    else if (best) { gx = best.x; gz = best.z; }
    else if (CHASE && opp) {
      const dx = ship.x - opp.x, dz = ship.z - opp.z, d = Math.hypot(dx, dz) || 1;
      gx = opp.x + (dx / d) * 14 + (-dz / d) * 8; gz = opp.z + (dz / d) * 14 + (dx / d) * 8;
    } else {
      const a = elapsed * 0.25;
      const r = Math.min(35, radius - 8);
      gx = Math.sin(a) * r; gz = Math.cos(a) * r;
    }
    const dx = gx - ship.x, dz = gz - ship.z, d = Math.hypot(dx, dz);
    const speed = STAY ? 0 : Math.min(11, d / Math.max(dt, 0.001));
    ship.vx = d > 0.01 ? (dx / d) * speed : 0;
    ship.vz = d > 0.01 ? (dz / d) * speed : 0;
    if (d > 0.3 && !STAY) ship.yaw = (Math.atan2(dx, dz) * 180 / Math.PI + 360) % 360;
    ship.x += ship.vx * dt;
    ship.z += ship.vz * dt;
    // claim barrels in reach (the server checks our last state: send it first)
    for (const b of barrels.values()) {
      if (Math.hypot(b.x - ship.x, b.z - ship.z) <= 5 && !b.claimed) { b.claimed = true; send(stateMsg()); send({ t: "pickup", id: b.id }); }
    }
    // storm
    if (Math.hypot(ship.x, ship.z) > radius) {
      outsideFor += dt;
      ship.hp -= (match.storm.damagePerSecond + match.storm.damageGrowthPerSecond * outsideFor) * dt;
      if (ship.hp <= 0) sink("storm");
    } else outsideFor = 0;
    // a hit on the other ship every second
    hitTimer -= dt;
    if (!NO_HITS && hitTimer <= 0) { hitTimer = 1; send({ t: "hit", dmg: HIT_DAMAGE, weaponId: "fake_cannon" }); dealt += HIT_DAMAGE; }
  }
  stateTimer -= dt;
  if (stateTimer <= 0) { stateTimer = 1 / 15; send(stateMsg()); }
}, 33);

setInterval(() => {
  if (match && !ended && now() >= match.startsAt)
    log(`t=${((now() - match.startsAt) / 1000).toFixed(0)}s pos (${ship.x.toFixed(1)}, ${ship.z.toFixed(1)}) hp ${ship.hp.toFixed(1)}/${ship.maxHp} lvl ${ship.level} dealt ${dealt} taken ${taken.toFixed(1)}` +
      (opp ? ` | opp (${opp.x.toFixed(1)}, ${opp.z.toFixed(1)}) hp ${opp.hp}/${opp.maxHp} lvl ${opp.level}` : ""));
}, 5000);
