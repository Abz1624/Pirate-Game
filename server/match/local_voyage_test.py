"""V5 M9b part 2a: the scripted server voyage against the match server image in LOCAL Docker (no Railway, no registry).

- A random ticket secret for this run only (never printed); the API runs locally (SQLite) and issues the tickets through
  POST /api/lobbies/voyage/ticket, so the endpoint and the Unity verifier are checked together.
- N match servers (default 1) from rogue-pirates-match:local, each with its own scripted headless test client (the same
  image with -rpClient ws://host.docker.internal:<port>/, i.e. through the published localhost port).
- Server 1 also gets a second player's ticket (refused: one voyage per process), its first ticket again (refused:
  replayed), a socket drop at --drop-at (resume measured) and, at the end, `docker stop`: the client must see end
  ServerClosing (or a clean disconnect) and return to the lobby.
- docker stats every 2 s; cold start = `docker run` to the server's READY log line.

Output: <out>/summary.json, and per server server-N/{server.json, client.json, stats.csv, server.log, client.log}.
Usage:  python server/match/local_voyage_test.py [--seconds 90] [--servers 1] [--out DIR]
"""
import argparse
import base64
import datetime as dt
import json
import os
import re
import secrets
import subprocess
import sys
import tempfile
import time
import urllib.request
import uuid

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DOCKER_BIN = r"C:\Program Files\Docker\Docker\resources\bin"
DOTNET_BIN = r"C:\Program Files\dotnet"


def log(message):
    print(f"[{dt.datetime.now():%H:%M:%S}] {message}", flush=True)


def run(args, env=None, check=True, timeout=600):
    r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env, timeout=timeout)
    if check and r.returncode != 0:
        raise RuntimeError(f"{args[0]} {args[1]} failed ({r.returncode}): {r.stderr.strip()[:500]}")
    return r


def docker(*args, env=None, check=True, timeout=600):
    return run(["docker", *args], env=env, check=check, timeout=timeout)


def logs(name):
    r = docker("logs", name, check=False)
    return r.stdout + r.stderr


def http(method, url, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    with urllib.request.urlopen(req, timeout=10) as res:
        return json.loads(res.read().decode() or "null")


def read_json(path):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def mem_mb(text):
    m = re.match(r"([\d.]+)\s*(GiB|MiB|KiB|B)", text.strip())
    if not m:
        return 0.0
    v = float(m.group(1))
    return {"GiB": v * 1024, "MiB": v, "KiB": v / 1024, "B": v / 1048576}[m.group(2)]


def stats_of(values):
    return {"max": round(max(values), 1), "mean": round(sum(values) / len(values), 1), "samples": len(values)} if values else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seconds", type=int, default=90)
    ap.add_argument("--drop-at", type=int, default=45)
    ap.add_argument("--servers", type=int, default=1)
    ap.add_argument("--drop-during-cards", action="store_true", help="server 1's client also drops its socket once while level-up cards are up (M9b review)")
    ap.add_argument("--away-during-cards", type=int, default=0, help="seconds server 1's client is away (page hidden, says so) while a later card set is up (M9b playtest fixes)")
    ap.add_argument("--hidden-during-cards", type=int, default=0, help="seconds server 1's client runs as a hidden page that never said so (one frame a second) while a later card set is up; over 30 s the server's card timeout fires (M9b playtest fixes)")
    ap.add_argument("--image", default="rogue-pirates-match:local")
    ap.add_argument("--out", default="")
    ap.add_argument("--base-port", type=int, default=17777)
    ap.add_argument("--api-port", type=int, default=18080)
    a = ap.parse_args()

    os.environ["PATH"] = DOCKER_BIN + os.pathsep + DOTNET_BIN + os.pathsep + os.environ["PATH"]
    out = a.out or os.path.join(REPO, "plan", "v5", "phase-09b-netcode", "docker", "run-" + dt.datetime.now().strftime("%Y%m%d-%H%M%S"))
    os.makedirs(out, exist_ok=True)
    out = os.path.abspath(out)

    # The ticket secret: this run only, in the environment of the API and the containers (`-e NAME` passes it without
    # putting it on a command line); never printed or written.
    env = dict(os.environ)
    env["RP_MATCH_TICKET_SECRET"] = secrets.token_urlsafe(36)
    match_id = "local-" + uuid.uuid4().hex[:8]

    api_bin = os.path.join(tempfile.gettempdir(), "rp-match-test-api")
    api_db = os.path.join(tempfile.gettempdir(), f"rp-match-test-{match_id}.db")
    log(f"building the API into {api_bin}")
    run(["dotnet", "build", os.path.join(REPO, "server", "RoguePirates.Api"), "-c", "Release", "-o", api_bin, "--nologo", "-v", "q"])
    api_env = dict(env, PORT=str(a.api_port), SQLITE_PATH=api_db, VOYAGE_SERVER_URL=f"ws://localhost:{a.base_port}/",
                   VOYAGE_MATCH_ID=match_id, RATE_LIMIT_PER_MINUTE="1000")
    api_env.pop("DATABASE_URL", None)
    api_log = open(os.path.join(out, "api.log"), "w")
    api = subprocess.Popen(["dotnet", os.path.join(api_bin, "RoguePirates.Api.dll")], cwd=api_bin, env=api_env, stdout=api_log, stderr=subprocess.STDOUT)
    api_url = f"http://127.0.0.1:{a.api_port}"
    deadline = time.time() + 180
    while True:
        try:
            if http("GET", api_url + "/health").get("ok"):
                break
        except Exception:
            pass
        if time.time() > deadline:
            raise RuntimeError("the API did not start (api.log)")
        time.sleep(0.5)
    log("API up")

    def new_player(name):
        return http("POST", api_url + "/api/profiles", {"name": name})["token"]

    def ticket(token):
        return http("POST", api_url + "/api/lobbies/voyage/ticket", token=token)

    containers = []
    try:
        servers = []
        for i in range(1, a.servers + 1):
            name, port = f"rp-match-test-{i}", a.base_port + i - 1
            d = os.path.join(out, f"server-{i}")
            os.makedirs(d, exist_ok=True)
            docker("rm", "-f", name, check=False)
            t0 = time.time()
            docker("run", "-d", "--name", name, "-p", f"{port}:7777", "-e", "RP_MATCH_TICKET_SECRET", "-e", f"RP_MATCH_ID={match_id}",
                   "-e", "RP_METRICS_DIR=/metrics", "-v", f"{d}:/metrics", a.image, env=env)
            containers.append(name)
            servers.append({"i": i, "name": name, "port": port, "dir": d, "t0": t0})
        for s in servers:
            deadline = time.time() + 180
            while True:
                text = logs(s["name"])
                if "[V5 Net] READY" in text:
                    s["cold"] = round(time.time() - s["t0"], 2)
                    m = re.search(r"READY .* after ([\d.]+) s in the process", text)
                    s["inProcess"] = float(m.group(1)) if m else None
                    break
                if re.search(r"EXIT \d", text) or time.time() > deadline:
                    raise RuntimeError(f"{s['name']} did not get ready:\n{text[-2000:]}")
                time.sleep(0.1)
            log(f"{s['name']} READY on :{s['port']} after {s['cold']} s (process {s['inProcess']} s)")

        first_ticket = None
        for s in servers:
            t = ticket(new_player(f"Tester{s['i']}"))
            if s["i"] == 1:
                first_ticket = t["ticket"]
                log(f"ticket from the API: matchServerUrl={t['matchServerUrl']} matchId={t['matchId']} lifetime={t['expiresAt'] - int(time.time())} s")
            cname = f"rp-match-client-{s['i']}"
            docker("rm", "-f", cname, check=False)
            docker("run", "-d", "--name", cname, "-e", "RP_JOIN_TICKET", "-v", f"{s['dir']}:/out", a.image,
                   "-rpClient", f"ws://host.docker.internal:{s['port']}/", "-rpSeconds", str(a.seconds),
                   "-rpDropAt", str(a.drop_at if s["i"] == 1 else 0), "-rpWaitEnd", "60", "-rpOut", "/out/client.json",
                   *(["-rpDropDuringCards"] if a.drop_during_cards and s["i"] == 1 else []),
                   *(["-rpAwayDuringCards", str(a.away_during_cards)] if a.away_during_cards and s["i"] == 1 else []),
                   *(["-rpHiddenDuringCards", str(a.hidden_during_cards)] if a.hidden_during_cards and s["i"] == 1 else []),
                   env=dict(env, RP_JOIN_TICKET=t["ticket"]))
            containers.append(cname)

        extra = {}
        deadline = time.time() + 120
        while "welcomed in" not in logs("rp-match-client-1"):
            if time.time() > deadline:
                raise RuntimeError("client 1 never joined:\n" + logs("rp-match-client-1")[-2000:])
            time.sleep(0.25)
        time.sleep(10)
        for key, tk in (("secondPlayer", ticket(new_player("Intruder"))["ticket"]), ("replayedTicket", first_ticket)):
            r = docker("run", "--rm", "-e", "RP_JOIN_TICKET", a.image, "-rpClient", f"ws://host.docker.internal:{a.base_port}/",
                       "-rpSeconds", "1", "-rpDropAt", "0", "-rpWaitEnd", "1", "-rpOut", "/tmp/c.json",
                       env=dict(env, RP_JOIN_TICKET=tk), check=False, timeout=180)
            line = next((l.strip() for l in (r.stdout + r.stderr).splitlines() if "[V5 NetTest] EXIT" in l), "no EXIT line")
            extra[key] = line
            log(f"{key}: {line}")

        samples = {s["name"]: [] for s in servers}
        while True:
            r = docker("stats", "--no-stream", "--format", "{{.Name}};{{.MemUsage}};{{.CPUPerc}}", *samples.keys(), check=False)
            now = dt.datetime.now().strftime("%H:%M:%S")
            for line in r.stdout.splitlines():
                p = line.split(";")
                if len(p) >= 3 and p[0] in samples:
                    samples[p[0]].append((now, round(mem_mb(p[1].split("/")[0]), 1), float(p[2].rstrip("%") or 0)))
            if re.search(r"VOYAGE-DONE|EXIT", logs("rp-match-client-1")):
                break
            time.sleep(2)

        log("voyage done; docker stop on the servers")
        stop_utc, stop_seconds = {}, {}
        for s in servers:
            stop_utc[s["name"]] = dt.datetime.now(dt.timezone.utc)
            t = time.time()
            docker("stop", "-t", "15", s["name"])
            stop_seconds[s["name"]] = round(time.time() - t, 2)
        for s in servers:
            docker("wait", f"rp-match-client-{s['i']}", timeout=120)

        results = []
        for s in servers:
            d = s["dir"]
            with open(os.path.join(d, "server.log"), "w", encoding="utf-8") as f:
                f.write(logs(s["name"]))
            with open(os.path.join(d, "client.log"), "w", encoding="utf-8") as f:
                f.write(logs(f"rp-match-client-{s['i']}"))
            with open(os.path.join(d, "stats.csv"), "w", encoding="utf-8") as f:
                f.write("time,memMB,cpuPercent\n" + "".join(f"{t},{m},{c}\n" for t, m, c in samples[s["name"]]))
            server = read_json(os.path.join(d, "server.json")) or {}
            client = read_json(os.path.join(d, "client.json")) or {}
            sec = (server.get("sections") or [{}])[0]
            csec = (client.get("sections") or [{}])[0]
            lobby_after = None
            if client.get("lobbyUtc") and client.get("endReason") in ("ServerClosing", "", None):
                # .NET "o" in UTC: 2026-10-08T09:30:00.1234567Z (7 digits); Python takes 6.
                lobby = dt.datetime.fromisoformat(client["lobbyUtc"][:26] + "+00:00")
                lobby_after = round((lobby - stop_utc[s["name"]]).total_seconds(), 3)
            inspect = lambda n: docker("inspect", "-f", "{{.State.ExitCode}}", n).stdout.strip()
            results.append({
                "server": s["name"], "port": s["port"],
                "coldStartSeconds": s["cold"], "processStartToListeningSeconds": s["inProcess"],
                "serverExitCode": inspect(s["name"]), "clientExitCode": inspect(f"rp-match-client-{s['i']}"),
                "dockerStopSeconds": stop_seconds[s["name"]], "secondsFromDockerStopToClientLobby": lobby_after,
                "serverCloseReason": server.get("closeReason"), "serverReportKind": server.get("kind"),
                "serverBackend": server.get("scriptingBackend"), "serverRssMB": server.get("rssMB"),
                "tickMs": sec.get("tickMs"), "serverTraffic": sec.get("clients"),
                "serverSeconds": sec.get("seconds"),
                "serverBytesPerSecondAllSockets": {k: round(sum(c.get(k, 0) for c in sec.get("clients") or []), 1) for k in
                                                   ("bytesDownPerSecond", "bytesUpPerSecond", "wireDownPerSecondEstimate", "wireUpPerSecondEstimate")}, "snapshotBytes": sec.get("snapshotBytes"),
                "snapshotEnemies": sec.get("snapshotEnemies"), "guessedInputs": sec.get("guessedInputs"),
                "serverResumeSeconds": sec.get("resumeSeconds"),
                "dockerMemMB": stats_of([m for _, m, _ in samples[s["name"]]]),
                "dockerCpuPercent": stats_of([c for _, _, c in samples[s["name"]]]),
                "client": {k: client.get(k) for k in ("ok", "outcome", "joinSeconds", "resumeSeconds", "endedByServer", "endReason",
                                                       "lobbyUtc", "snapshots", "kills", "level", "finalHull", "cardSets", "cardsPicked",
                                                       "droppedDuringCards", "resumeDuringCardsSeconds", "cardsBackAfterResume",
                                                       "awayDuringCardsSeconds", "awayWelcomeSeconds", "cardsBackAfterAway",
                                                       "hiddenDuringCardsSeconds", "hiddenCardsResolution", "hiddenControlBackSeconds",
                                                       "lateResyncs", "awaysSent", "resyncs", "enemyJumpsMarked", "events")} | {
                    "traffic": csec.get("clients"), "predictionErrorMetres": csec.get("predictionErrorMetres"),
                    "correctionMetres": csec.get("correctionMetres"), "inputLagMs": csec.get("inputLagMs")},
            })
        summary = {
            "utc": dt.datetime.now(dt.timezone.utc).isoformat(), "image": a.image,
            "imageId": docker("image", "inspect", "-f", "{{.Id}}", a.image).stdout.strip(),
            "seconds": a.seconds, "dropAt": a.drop_at, "servers": a.servers,
            "docker": docker("version", "-f", "{{.Server.Version}}").stdout.strip(),
            "extraChecks": extra, "results": results,
        }
        with open(os.path.join(out, "summary.json"), "w", encoding="utf-8") as f:
            json.dump(summary, f, indent=2)
        log("summary: " + os.path.join(out, "summary.json"))
    finally:
        for c in containers:
            docker("rm", "-f", c, check=False)
        api.terminate()
        try:
            api.wait(10)
        except subprocess.TimeoutExpired:
            api.kill()
        api_log.close()
        for f in (api_db, api_db + "-wal", api_db + "-shm"):
            try:
                os.remove(f)
            except OSError:
                pass


if __name__ == "__main__":
    sys.exit(main())
