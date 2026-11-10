#!/usr/bin/env bash
# Connect the Gemini CLI to the NotDory MCP server: writes an mcpServers.notdory entry (httpUrl, headers) in the Gemini CLI's user settings.json.
# Config: ~/.gemini/settings.json
# Idempotent: an existing 'notdory' entry is replaced; every other server is kept.
# Authenticates with the credential ACCESS KEY only: Claude Code, Codex, Cursor and the Gemini CLI
# send it in the x-access-key header; Mux sends it as a bearer token (Authorization: Bearer <key>),
# as docs/INSTRUCTIONS_FOR_MUX.md describes. The secret key is never sent. The access key is a
# capability token, so use a least-privilege credential.
# WSL 1 version: configures the Gemini CLI inside this distro (not on Windows). For the Gemini CLI on Windows, run
# scripts\windows\install-gemini.bat from Windows.
# WSL 1 shares localhost with Windows, so 127.0.0.1 also reaches a server running on Windows.
# Usage: scripts/wsl/install-gemini.sh [ACCESS_KEY]
#   ACCESS_KEY             overrides NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
#   NOTDORY_MCP_URL        server URL (default: http://127.0.0.1:8720/mcp)
#   NOTDORY_GEMINI_CONFIG  config file (default: as above)
# Requires python3.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -le 1 ] || die "Unexpected argument: $2 (usage: install-gemini.sh [ACCESS_KEY])"
TOKEN="${1:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
wsl_require_cmd python3 "Install Python 3 inside WSL (for example: sudo apt install python3)."
CONFIG="${NOTDORY_GEMINI_CONFIG:-$HOME/.gemini/settings.json}"
# The same rule as the Windows scripts: http:// or https://, a host, and no whitespace.
URL_RE='^https?://[^/?#[:space:]]+[^[:space:]]*$'
check_url() { [[ "$1" =~ $URL_RE ]] || die "'$1' is not an http(s) URL."; }
if [ -n "${NOTDORY_MCP_URL:-}" ]; then
    URL="$NOTDORY_MCP_URL"
else
    # WSL 1 shares localhost with Windows, so wsl_resolve_local_url leaves the URL unchanged here.
    URL="$(wsl_resolve_local_url "http://127.0.0.1:8720/mcp")"
fi
check_url "$URL"
wsl_log "This configures the Gemini CLI inside this WSL distro. For the Gemini CLI on Windows, run scripts\\windows\\install-gemini.bat from Windows."

python3 - install "$CONFIG" notdory httpUrl "$URL" "x-access-key" "$TOKEN" <<'PY'
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
action, path, name, url_key, url, hname, hvalue = sys.argv[1:8]
def servers_of(cfg):
    check_case(cfg, "mcpServers", "The top-level object", path)
    servers = cfg.get("mcpServers")
    if servers is None:
        return None
    if not isinstance(servers, dict):
        fail("mcpServers in %s is not a JSON object; it was left unchanged." % path)
    check_case(servers, name, "mcpServers", path)
    return servers
cfg = load(path)
servers = servers_of(cfg)
messages = []
present = servers is not None and name in servers
if action in ("remove", "reinstall"):
    if present:
        del servers[name]
        save(path, cfg)
        messages.append("Removed '%s' from %s." % (name, path))
    else:
        messages.append("No '%s' entry in %s; %s." % (name, path, "nothing to remove" if action == "remove" else "continuing"))
if action in ("install", "reinstall"):
    if servers is None:
        servers = cfg["mcpServers"] = {}
    entry = {url_key: url}
    if hname:
        entry["headers"] = {hname: hvalue}
    servers[name] = entry
    save(path, cfg)
    if present and action == "install":
        messages.append("Replaced the existing '%s' entry in %s (%s)." % (name, path, url))
    else:
        messages.append("Added '%s' to %s (%s)." % (name, path, url))
print("\n".join(messages))
PY
echo "Restart the Gemini CLI to pick up the change (run /mcp to check)."
