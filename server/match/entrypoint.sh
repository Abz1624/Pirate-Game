#!/bin/sh
# PID 1 of the match server image. Starts the Unity server headless (logs to stdout) and turns SIGTERM/SIGINT (docker
# stop, a Railway redeploy or scale-down) into the stop file the server polls (NetServerHost.StopFile): the server sends
# the connected player `end` ServerClosing, gives the transport a moment, writes its last report and exits. Exits with
# the server's own exit code.
set -u
STOP=/tmp/rp-match-stop
rm -f "$STOP" /tmp/rp-match-ready
/app/RoguePirates.x86_64 -batchmode -nographics -logFile - "$@" &
pid=$!
trap 'echo "[entrypoint] stop signal: asking the match server to close"; touch "$STOP"' TERM INT
code=0
while :; do
  wait "$pid"
  code=$?
  kill -0 "$pid" 2>/dev/null || break
done
echo "[entrypoint] match server exited with $code"
exit "$code"
