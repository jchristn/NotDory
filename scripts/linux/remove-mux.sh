#!/usr/bin/env bash
# Disconnect Mux from the NotDory MCP server: removes the 'notdory' entry.
# Config: ${MUX_CONFIG_DIR:-~/.mux}/mcp-servers.json
# Succeeds with "nothing to remove" when there is no 'notdory' entry; every other server is kept.
# Usage: scripts/linux/remove-mux.sh
#   NOTDORY_MUX_CONFIG  config file (default: as above)
# Requires python3.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/macos on macOS or scripts\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL (Docker Desktop containers have one too); WSL also has interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1); they handle the Windows PATH, line endings and the WSL 2 network."
fi

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments; see --help)"
command -v python3 >/dev/null 2>&1 || die "'python3' was not found. Install Python 3 with your package manager (for example: sudo apt install python3)."
CONFIG="${NOTDORY_MUX_CONFIG:-${MUX_CONFIG_DIR:-$HOME/.mux}/mcp-servers.json}"
URL=""
BASE_URL=""

python3 - remove "$CONFIG" notdory "$BASE_URL" "$URL" bearer "" "" <<'PY'
import json, os, re, sys, tempfile
def fail(msg):
    print("Error: " + msg, file=sys.stderr)
    sys.exit(1)
def mode_of(path, default):
    try:
        return os.stat(path).st_mode & 0o7777
    except FileNotFoundError:
        return default
def write_atomic(path, text, new_mode=0o600, force_mode=None):
    """Replaces the file in one step. Follows a symlinked config and keeps the file's mode."""
    path = os.path.realpath(path)
    folder = os.path.dirname(path) or "."
    os.makedirs(folder, exist_ok=True)
    mode = force_mode if force_mode is not None else mode_of(path, new_mode)
    fd, tmp = tempfile.mkstemp(prefix="." + os.path.basename(path) + ".", suffix=".tmp", dir=folder)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        os.chmod(tmp, mode)
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.unlink(tmp)
        raise
def read_text(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return f.read()
def check_case(obj, key, where, path):
    for k in obj:
        if k != key and k.lower() == key.lower():
            fail("%s of %s has a key '%s' that differs from '%s' only in case; rename or remove it, then run this again. The file was left unchanged." % (where, path, k, key))
def load(path):
    """The config as a dict; {} when the file is missing or empty. Never guesses at a broken file."""
    if not os.path.exists(path):
        return {}
    text = read_text(path)
    if not text.strip():
        return {}
    try:
        cfg = json.loads(text)
    except ValueError as e:
        fail("%s is not valid JSON (%s). Fix or move it, then run this again; it was left unchanged." % (path, e))
    if not isinstance(cfg, dict):
        fail("%s does not contain a JSON object; it was left unchanged." % path)
    return cfg
def save(path, cfg):
    write_atomic(path, json.dumps(cfg, indent=2) + "\n")
action, path, name, base, url, auth_type, hname, token = sys.argv[1:9]
if action != "remove":
    if base:
        base, mcp_path = base.rstrip("/"), "/mcp"
    else:
        m = re.match(r"(https?://[^/?#]+)([^?#]*)(\?[^#]*)?", url)
        base = m.group(1)
        mcp_path = (m.group(2) if m.group(2) not in ("", "/") else "/mcp") + (m.group(3) or "")
def servers_of(cfg):
    check_case(cfg, "servers", "The top-level object", path)
    servers = cfg.get("servers")
    if servers is None:
        return None
    if not isinstance(servers, list):
        fail("servers in %s is not a JSON array; it was left unchanged." % path)
    return servers
def is_ours(server):
    return isinstance(server, dict) and server.get("name") == name
cfg = load(path)
servers = servers_of(cfg)
messages = []
present = servers is not None and any(is_ours(s) for s in servers)
if action in ("remove", "reinstall"):
    if present:
        cfg["servers"] = servers = [s for s in servers if not is_ours(s)]
        save(path, cfg)
        messages.append("Removed '%s' from %s." % (name, path))
    else:
        messages.append("No '%s' entry in %s; %s." % (name, path, "nothing to remove" if action == "remove" else "continuing"))
if action in ("install", "reinstall"):
    if auth_type == "bearer":
        auth = {"type": "bearer", "bearerToken": token, "apiKeyHeader": "X-API-Key", "apiKeyValue": ""}
    elif auth_type == "apikey":
        auth = {"type": "apikey", "bearerToken": "", "apiKeyHeader": hname, "apiKeyValue": token}
    else:
        auth = {"type": "none", "bearerToken": "", "apiKeyHeader": "X-API-Key", "apiKeyValue": ""}
    entry = {"name": name, "transport": "http", "command": "", "args": [], "env": {}, "url": base, "mcpPath": mcp_path, "auth": auth}
    if servers is None:
        servers = cfg["servers"] = []
    # Replace an existing entry in place (dropping any duplicates); otherwise append.
    kept, replaced = [], False
    for s in servers:
        if is_ours(s):
            if not replaced:
                kept.append(entry)
                replaced = True
        else:
            kept.append(s)
    if not replaced:
        kept.append(entry)
    cfg["servers"] = kept
    save(path, cfg)
    if present and action == "install":
        messages.append("Replaced the existing '%s' entry in %s (%s)." % (name, path, base + mcp_path))
    else:
        messages.append("Added '%s' to %s (%s)." % (name, path, base + mcp_path))
print("\n".join(messages))
PY
echo "Restart Mux to pick up the change."
