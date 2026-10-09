# V5 M9b: drives one scripted voyage against the Railway match server from this PC, using the local image as the
# client. Gets a guest profile and a ticket from the live API (neither is printed), then runs the client container
# over wss://. Results go to plan/v5/phase-09b-netcode/railway/<name>/.
# Usage: python server/match/railway_voyage_test.py --name voyage-90s [--seconds 90] [--drop-at 45] [--drop-during-cards]
import argparse, json, os, subprocess, sys, time, urllib.request, urllib.error

API = "https://api-production-dad8.up.railway.app"
DOCKER = r"C:\Program Files\Docker\Docker\resources\bin\docker.exe"
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def post(path, body, token=None):
    req = urllib.request.Request(API + path, data=json.dumps(body).encode(), method="POST",
                                 headers={"Content-Type": "application/json", "Origin": "http://localhost",
                                          **({"Authorization": "Bearer " + token} if token else {})})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read())


def main():
    a = argparse.ArgumentParser()
    a.add_argument("--name", required=True)
    a.add_argument("--seconds", type=int, default=90)
    a.add_argument("--drop-at", type=int, default=45)
    a.add_argument("--drop-during-cards", action="store_true")
    a.add_argument("--image", default="rogue-pirates-match:local")
    a.add_argument("--wait-api", type=int, default=300, help="seconds to wait for the ticket endpoint")
    args = a.parse_args()

    out = os.path.join(REPO, "plan", "v5", "phase-09b-netcode", "railway", args.name)
    os.makedirs(out, exist_ok=True)
    profile = post("/api/profiles", {"name": "NetTest"})
    deadline = time.time() + args.wait_api
    while True:
        try:
            t = post("/api/lobbies/voyage/ticket", {}, profile["token"])
            break
        except urllib.error.HTTPError as e:
            if time.time() > deadline:
                sys.exit(f"ticket endpoint still {e.code} after {args.wait_api} s")
            time.sleep(5)
    url = t["matchServerUrl"]
    print(f"ticket ok; server {url}; match {t['matchId']}")
    env = dict(os.environ, RP_JOIN_TICKET=t["ticket"])
    cmd = [DOCKER, "run", "--rm", "-e", "RP_JOIN_TICKET", "-v", f"{out}:/out", args.image, "-rpClient", url,
           "-rpSeconds", str(args.seconds), "-rpDropAt", str(args.drop_at), "-rpWaitEnd", "60",
           "-rpOut", "/out/client.json", *(["-rpDropDuringCards"] if args.drop_during_cards else [])]
    started = time.time()
    with open(os.path.join(out, "client.log"), "w", encoding="utf-8") as log:
        code = subprocess.call(cmd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=args.seconds + 240)
    print(f"client exit {code} after {time.time() - started:.0f} s; results in {out}")
    p = os.path.join(out, "client.json")
    if os.path.exists(p):
        r = json.load(open(p, encoding="utf-8"))
        keep = {k: r[k] for k in r if not isinstance(r[k], (list, dict))}
        print(json.dumps(keep, indent=1)[:3000])


if __name__ == "__main__":
    main()
