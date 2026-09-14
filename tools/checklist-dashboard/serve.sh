#!/usr/bin/env bash
# Serves the checklist dashboard and repo markdown files on http://localhost:8080
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT" || exit 1

echo "Rogue Pirates Checklist Dashboard"
echo "  Root:      $ROOT"
echo "  Dashboard: http://localhost:8080/tools/checklist-dashboard/"
echo ""
echo "Press Ctrl+C to stop."
echo ""

python3 -m http.server 8080
