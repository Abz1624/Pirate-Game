"""Runs a C# snippet in the open Unity Editor (unity command eval_file) and prints its string result, unescaped.
Usage: python eval.py <file.cs> [timeout_seconds]   or   python eval.py -c "return 1 + 1;"
"""
import json
import os
import subprocess
import sys
import tempfile

args = sys.argv[1:]
if args and args[0] == "-c":
    fd, path = tempfile.mkstemp(suffix=".cs")
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(args[1])
    timeout = args[2] if len(args) > 2 else "60"
else:
    path = args[0]
    timeout = args[1] if len(args) > 1 else "60"

env = dict(os.environ)
env.setdefault("UNITY_PROJECT_PATH", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "Pirate Game Unity")))
proc = subprocess.run(["unity", "command", "eval_file", "--file", path, "--timeout", timeout, "--format", "json"],
                      capture_output=True, text=True, env=env, shell=(os.name == "nt"))
out = proc.stdout.strip()
try:
    data = json.loads(out[out.index("{"):])
    result = data.get("data", {}).get("result", data)
    if isinstance(result, dict):
        result = result.get("result", json.dumps(result))
    print(result)
except Exception:
    print(out or proc.stderr)
    sys.exit(1)
