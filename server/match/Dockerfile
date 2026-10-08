# syntax=docker/dockerfile:1.7
# Rogue Pirates match server (V5 M9b, I9b.1): the Unity project's Linux Dedicated Server build with the V5_NET define
# (RoguePirates.V5.Net.Editor.V5NetServerBuild), headless, one server-run voyage per process, plain WebSockets on $PORT
# (Railway's edge ends TLS). Built and tested in local Docker by server/match/build-image.ps1 and pushed to GHCR by
# push-image.ps1; Railway only runs the finished image (owner, 8 Oct: Railway never builds it).
#
# Build context: "Pirate Game Unity/Builds/LinuxServer" (the Unity build output); the entry script comes from the named
# context "match" (this folder):
#   docker build -f server/match/Dockerfile --build-context match=server/match -t rogue-pirates-match:local "Pirate Game Unity/Builds/LinuxServer"
#
# Environment: PORT (default 7777), RP_MATCH_TICKET_SECRET (required, 32+ characters, the API's ticket secret; never
# logged), RP_MATCH_ID (optional: only tickets for this match id join), RP_METRICS_DIR (optional: the server writes
# server.json there every 10 s), RP_SERVER_FPS (optional, default 60).
FROM debian:bookworm-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && useradd --create-home --uid 10001 --shell /usr/sbin/nologin rp

WORKDIR /app
# Root-owned and read-only for the server's user: the player writes only to its home (Unity's config folder) and /tmp.
COPY --chmod=0755 . /app/
COPY --from=match --chmod=0755 entrypoint.sh /usr/local/bin/rp-match-entrypoint
# A Windows checkout may have given the script CRLF line ends (core.autocrlf); sh needs LF.
RUN sed -i 's/\r$//' /usr/local/bin/rp-match-entrypoint

ENV PORT=7777
EXPOSE 7777
USER rp

# Ready = the server listens (it writes /tmp/rp-match-ready after StartListening and removes it when closing).
# Docker's own health status; Railway's HTTP health check cannot speak to a WebSocket-only server (leave it unset).
HEALTHCHECK --interval=5s --timeout=2s --start-period=60s --retries=3 CMD test -f /tmp/rp-match-ready || exit 1

ENTRYPOINT ["/usr/local/bin/rp-match-entrypoint"]
# The match server. The same image runs the scripted test client with: -rpClient ws://host:port/ (RP_JOIN_TICKET in the env).
CMD ["-rpServer"]
