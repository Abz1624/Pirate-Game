"""V5 M9b part 2b: a LOCAL server-voyage session for the browser (or Editor) client. Nothing on Railway or a registry.

Starts, with a random ticket secret for this session only (never printed or written):
- the API, built and run locally on SQLite (the guest profiles and POST /api/lobbies/voyage/ticket), which names the
  match server ws://localhost:<match-port>/ in every ticket;
- the match server from the local image (rogue-pirates-match:local) in Docker with `--restart always`, as the Railway
  service will run (the process exits after each voyage and a fresh one starts);
- optionally a static server for a WebGL build (--webgl <folder>), and prints the URL to open:
  http://localhost:<web-port>/?api=http://localhost:<api-port>&netvoyage=1   (or netvoyage=auto&seconds=N, scripted).
The Editor client: set the API override (PlayerPrefs rp.online.apiBaseUrl) to the printed API URL, then start
RoguePirates.V5.Net.NetVoyageClient.Begin() in Play mode.

Runs until --minutes pass or Ctrl+C; then collects the match server's log into --out and removes the container.
Usage: python server/match/local_session.py [--webgl "Pirate Game Unity/Builds/WebGL-Desktop-Net/RoguePirates"] [--minutes 30]
"""
import argparse
import datetime as dt
import os
import secrets
import subprocess
import sys
import tempfile
import time
import urllib.request
import json
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--image", default="rogue-pirates-match:local")
    ap.add_argument("--match-port", type=int, default=17800)
    ap.add_argument("--api-port", type=int, default=18100)
    ap.add_argument("--web-port", type=int, default=8090)
    ap.add_argument("--webgl", default="")
    ap.add_argument("--minutes", type=float, default=60)
    ap.add_argument("--out", default="")
    a = ap.parse_args()

    os.environ["PATH"] = DOCKER_BIN + os.pathsep + DOTNET_BIN + os.pathsep + os.environ["PATH"]
    out = os.path.abspath(a.out or os.path.join(REPO, "plan", "v5", "phase-09b-netcode", "session", "session-" + dt.datetime.now().strftime("%Y%m%d-%H%M%S")))
    os.makedirs(out, exist_ok=True)
    env = dict(os.environ)
    env["RP_MATCH_TICKET_SECRET"] = secrets.token_urlsafe(36)
    match_id = "local-" + uuid.uuid4().hex[:8]

    api_bin = os.path.join(tempfile.gettempdir(), "rp-match-session-api")
    api_db = os.path.join(tempfile.gettempdir(), f"rp-match-session-{match_id}.db")
    log("building the API")
    run(["dotnet", "build", os.path.join(REPO, "server", "RoguePirates.Api"), "-c", "Release", "-o", api_bin, "--nologo", "-v", "q"])
    api_env = dict(env, PORT=str(a.api_port), SQLITE_PATH=api_db, VOYAGE_SERVER_URL=f"ws://localhost:{a.match_port}/",
                   VOYAGE_MATCH_ID=match_id, RATE_LIMIT_PER_MINUTE="1000")
    api_env.pop("DATABASE_URL", None)
    api_log = open(os.path.join(out, "api.log"), "w")
    api = subprocess.Popen(["dotnet", os.path.join(api_bin, "RoguePirates.Api.dll")], cwd=api_bin, env=api_env, stdout=api_log, stderr=subprocess.STDOUT)
    web = None
    name = "rp-match-session"
    try:
        api_url = f"http://localhost:{a.api_port}"
        deadline = time.time() + 180
        while True:
            try:
                with urllib.request.urlopen(api_url + "/health", timeout=3) as res:
                    if json.loads(res.read().decode()).get("ok"):
                        break
            except Exception:
                pass
            if time.time() > deadline:
                raise RuntimeError("the API did not start (api.log)")
            time.sleep(0.5)
        log(f"API up at {api_url}")
        run(["docker", "rm", "-f", name], check=False)
        run(["docker", "run", "-d", "--name", name, "--restart", "always", "-p", f"{a.match_port}:7777", "-e", "RP_MATCH_TICKET_SECRET",
             "-e", f"RP_MATCH_ID={match_id}", a.image], env=env)
        deadline = time.time() + 120
        while "[V5 Net] READY" not in (run(["docker", "logs", name], check=False).stdout):
            if time.time() > deadline:
                raise RuntimeError("the match server did not get ready")
            time.sleep(0.2)
        log(f"match server READY on ws://localhost:{a.match_port}/ (match {match_id})")
        if a.webgl:
            folder = os.path.abspath(a.webgl)
            web = subprocess.Popen([sys.executable, os.path.join(REPO, "plan", "v5", "tools", "serve-webgl.py"), folder, str(a.web_port)],
                                   stdout=open(os.path.join(out, "web.log"), "w"), stderr=subprocess.STDOUT)
            log(f"WebGL client: http://localhost:{a.web_port}/?api={api_url}&netvoyage=1")
            log(f"scripted:     http://localhost:{a.web_port}/?api={api_url}&netvoyage=auto&seconds=120")
        log(f"Editor client: PlayerPrefs rp.online.apiBaseUrl = {api_url}, then NetVoyageClient.Begin() in Play mode")
        with open(os.path.join(out, "session.json"), "w", encoding="utf-8") as f:
            json.dump({"api": api_url, "match": f"ws://localhost:{a.match_port}/", "matchId": match_id,
                       "web": f"http://localhost:{a.web_port}/" if a.webgl else None, "image": a.image,
                       "started": dt.datetime.now(dt.timezone.utc).isoformat()}, f, indent=2)
        stop_file = os.path.join(out, "STOP")
        end = time.time() + a.minutes * 60
        while time.time() < end and not os.path.exists(stop_file):
            time.sleep(1)
    except KeyboardInterrupt:
        pass
    finally:
        with open(os.path.join(out, "server.log"), "w", encoding="utf-8") as f:
            r = run(["docker", "logs", name], check=False)
            f.write(r.stdout + r.stderr)
        run(["docker", "rm", "-f", name], check=False)
        if web:
            web.terminate()
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
        log("session over; logs in " + out)


if __name__ == "__main__":
    sys.exit(main())
