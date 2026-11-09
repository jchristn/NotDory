#!/usr/bin/env sh
# Disconnect Codex from NotDory by removing the [mcp_servers.notdory] table from ~/.codex/config.toml.
set -e

CONFIG="${NOTDORY_CODEX_CONFIG:-$HOME/.codex/config.toml}"
command -v python3 >/dev/null 2>&1 || { echo "python3 is required." >&2; exit 1; }

python3 - "$CONFIG" <<'PY'
import os, re, sys
path = sys.argv[1]
if not os.path.isfile(path):
    print("Nothing to remove at " + path); sys.exit(0)
with open(path, encoding="utf-8") as f:
    lines = f.read().splitlines()
out, skipping, found = [], False, False
for line in lines:
    m = re.match(r"\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$", line)
    if m:
        name = m.group(1).replace('"', "")
        skipping = name == "mcp_servers.notdory" or name.startswith("mcp_servers.notdory.")
        found = found or skipping
    if not skipping:
        out.append(line)
if not found:
    print("No 'notdory' entry found in " + path); sys.exit(0)
while out and not out[-1].strip():
    out.pop()
with open(path, "w", encoding="utf-8") as f:
    f.write("\n".join(out) + "\n")
print("Removed 'notdory' from " + path)
PY
