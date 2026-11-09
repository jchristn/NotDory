#!/usr/bin/env sh
# Connect Codex to the NotDory MCP server by writing an [mcp_servers.notdory] table to ~/.codex/config.toml.
# Authenticates with the credential ACCESS KEY only (x-access-key header); the secret key is never sent.
# Usage: install-codex.sh [ACCESS_KEY]  (arg #1 overrides NOTDORY_ACCESS_KEY)
# Override with NOTDORY_MCP_URL / NOTDORY_ACCESS_KEY / NOTDORY_CODEX_CONFIG.
set -e

URL="${NOTDORY_MCP_URL:-http://127.0.0.1:8720/mcp}"
AK="${1:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
CONFIG="${NOTDORY_CODEX_CONFIG:-$HOME/.codex/config.toml}"

command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" "$URL" "$AK" <<'PY'
import json, os, re, sys
path, url, ak = sys.argv[1], sys.argv[2], sys.argv[3]
d = os.path.dirname(path)
if d and not os.path.isdir(d):
    os.makedirs(d, exist_ok=True)
try:
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
except FileNotFoundError:
    lines = []
# Drop any existing [mcp_servers.notdory] table and its sub-tables; keep everything else verbatim.
out, skipping = [], False
for line in lines:
    m = re.match(r"\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$", line)
    if m:
        name = m.group(1).replace('"', "")
        skipping = name == "mcp_servers.notdory" or name.startswith("mcp_servers.notdory.")
    if not skipping:
        out.append(line)
while out and not out[-1].strip():
    out.pop()
if out:
    out.append("")
out += ["[mcp_servers.notdory]",
        "url = " + json.dumps(url),
        "http_headers = { " + json.dumps("x-access-key") + " = " + json.dumps(ak) + " }"]
with open(path, "w", encoding="utf-8") as f:
    f.write("\n".join(out) + "\n")
print("Added 'notdory' to " + path)
PY
echo "Restart Codex to pick up the change."
