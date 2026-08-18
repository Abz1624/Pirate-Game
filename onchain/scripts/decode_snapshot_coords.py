#!/usr/bin/env python3
import base64
import json
import struct
import subprocess
import sys

addr = sys.argv[1] if len(sys.argv) > 1 else "4fVrP5KqPcwM7KTpmUspe9jXkNmc8W5Pqjg37LxAYS4m"
rpc = sys.argv[2] if len(sys.argv) > 2 else "http://localhost:7799"

out = subprocess.check_output(
    ["solana", "account", addr, "-u", rpc, "--output", "json"],
    text=True,
)
d = base64.b64decode(json.loads(out)["account"]["data"][0])
print("len", len(d))
print("tick", int.from_bytes(d[40:44], "little"))
print("player_count", d[44], "enemy_count", d[45])
mp = d[50] if len(d) > 50 else 0
for i in range(min(mp, 4)):
    off = 56 + i * 18
    slot = d[off]
    x = struct.unpack_from("<h", d, off + 1)[0]
    z = struct.unpack_from("<h", d, off + 3)[0]
    print(f"player[{i}] slot={slot} x={x} z={z}")
