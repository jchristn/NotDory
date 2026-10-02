#!/usr/bin/env sh
# Connect Cursor to the NotDory MCP server by adding an 'notdory' entry to ~/.cursor/mcp.json.
# Authenticates with the credential ACCESS KEY only (x-access-key header); the secret key is never sent.
# Usage: install-cursor.sh [ACCESS_KEY]  (arg #1 overrides NOTDORY_ACCESS_KEY)
# Override with NOTDORY_MCP_URL / NOTDORY_ACCESS_KEY / NOTDORY_CURSOR_CONFIG.
set -e

URL="${NOTDORY_MCP_URL:-http://127.0.0.1:8720/mcp}"
AK="${1:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
CONFIG="${NOTDORY_CURSOR_CONFIG:-$HOME/.cursor/mcp.json}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$URL" "$AK" <<'PY'
import json, os, sys
path, url, ak = sys.argv[1], sys.argv[2], sys.argv[3]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        cfg = json.load(f)
    if not isinstance(cfg, dict):
        cfg = {}
except Exception:
    cfg = {}
servers = cfg.get("mcpServers")
if not isinstance(servers, dict):
    servers = {}
    cfg["mcpServers"] = servers
servers["notdory"] = {"url": url, "headers": {"x-access-key": ak}}
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
print("Added 'notdory' to " + path)
PY
echo "Restart Cursor to pick up the change (Settings -> MCP)."
