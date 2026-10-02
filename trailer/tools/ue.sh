#!/usr/bin/env bash
# Helpers for driving the open Unity Editor during trailer capture (source this file).
export UNITY_PROJECT_PATH="C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game\Pirate Game Unity"
TD="RoguePirates.V4.DevTools.Trailer.TrailerDirector"
TR="RoguePirates.V4.Editor.Trailer.TrailerRecorder"

# E 'C# expression returning a value' -> prints the result
E() {
  unity command eval --code "return $1;" 2>&1 | tail -1 | python -c "import sys,json
l=sys.stdin.read()
try:
  p=l.split('\t'); r=json.loads(p[2]); print(r.get('result') if r.get('success') else r)
except Exception: print(l.strip()[:400])"
}
play() { unity command editor_play >/dev/null 2>&1; for i in $(seq 1 40); do sleep 2; r=$(E "$TD.Status()" 2>/dev/null); case "$r" in t=*) echo "$r"; return 0;; esac; done; echo "run did not start: $r"; return 1; }
stop() { unity command editor_stop >/dev/null 2>&1; sleep 3; }
rec() { E "$TR.Start(\"$1\")"; }
recstop() { E "$TR.Stop()"; }
# take NAME WALL_SECONDS
take() { rec "$1"; sleep "$2"; recstop; }
# fresh SEED: reload the run on a seed and wait until it plays
fresh() { E "$TD.Restart($1)" >/dev/null; for i in $(seq 1 40); do sleep 2; r=$(E "$TD.Status()"); case "$r" in *state=Playing*) echo "$r"; return 0;; esac; done; echo "no run: $r"; return 1; }
# waitt SECONDS: wait until the run clock passes SECONDS (natural play)
waitt() { for i in $(seq 1 400); do r=$(E "$TD.Status()"); t=$(echo "$r" | sed -n 's/^t=\([0-9.]*\)s.*/\1/p'); [ -n "$t" ] && python -c "import sys; sys.exit(0 if float('$t') >= $1 else 1)" && { echo "$r"; return 0; }; sleep 3; done; echo "timeout: $r"; return 1; }
# Safer take: waits between commands, retries a start that did not take, verifies the stop.
take2() {
  sleep 2
  for a in 1 2 3; do r=$(rec "$1"); case "$r" in recording*) break;; esac; sleep 4; E "$TR.Stop()" >/dev/null; done
  echo "$r"; sleep "$2"
  for a in 1 2 3; do r=$(recstop); case "$r" in stopped*) break;; esac; sleep 4; if [ "$(E "$TR.IsRecording")" = "False" ]; then r="stopped (late) $1"; break; fi; done
  echo "$r"
}
