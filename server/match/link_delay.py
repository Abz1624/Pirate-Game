"""V5 M9b enemy stutter fix (10 Oct): a local TCP proxy that delays every byte by a fixed time each way (plus optional
jitter, order kept), so the browser client can be measured against the local match server at the owner's ~270 ms round
trip. The WebSocket runs over it unchanged. Local only; nothing on Railway or a registry.

Usage (standalone): python server/match/link_delay.py --listen 17800 --target 17801 --delay-ms 135 [--jitter-ms 15]
local_session.py starts it itself with --link-delay-ms.
"""
import argparse
import asyncio
import random
import threading
import time


async def _pump(reader, writer, delay, jitter, rng):
    queue = asyncio.Queue()
    last = [0.0]

    async def deliver():
        while True:
            at, data = await queue.get()
            if data is None:
                break
            wait = at - time.monotonic()
            if wait > 0:
                await asyncio.sleep(wait)
            writer.write(data)
            await writer.drain()
        try:
            writer.close()
        except Exception:
            pass

    task = asyncio.ensure_future(deliver())
    try:
        while True:
            data = await reader.read(65536)
            if not data:
                break
            at = max(last[0], time.monotonic() + delay + rng.uniform(0.0, jitter))
            last[0] = at
            queue.put_nowait((at, data))
    except Exception:
        pass
    queue.put_nowait((max(last[0], time.monotonic() + delay), None))
    await task


async def _serve(listen, target, delay, jitter, seed):
    rng = random.Random(seed)

    async def handle(client_reader, client_writer):
        try:
            server_reader, server_writer = await asyncio.open_connection("127.0.0.1", target)
        except Exception:
            client_writer.close()
            return
        await asyncio.gather(_pump(client_reader, server_writer, delay, jitter, rng),
                             _pump(server_reader, client_writer, delay, jitter, rng))

    server = await asyncio.start_server(handle, "127.0.0.1", listen)
    async with server:
        await server.serve_forever()


def start(listen, target, delay_ms, jitter_ms=0.0, seed=1):
    """Runs the proxy on a daemon thread; returns the thread."""
    thread = threading.Thread(target=lambda: asyncio.run(_serve(listen, target, delay_ms / 1000.0, jitter_ms / 1000.0, seed)), daemon=True)
    thread.start()
    return thread


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--listen", type=int, required=True)
    ap.add_argument("--target", type=int, required=True)
    ap.add_argument("--delay-ms", type=float, default=135)
    ap.add_argument("--jitter-ms", type=float, default=0)
    a = ap.parse_args()
    asyncio.run(_serve(a.listen, a.target, a.delay_ms / 1000.0, a.jitter_ms / 1000.0, 1))
