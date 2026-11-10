#!/usr/bin/env bash
# Install or remove the NotDory SessionStart hook in the Claude Code user settings (${CLAUDE_CONFIG_DIR:-~/.claude}/settings.json).
# At session start the hook fetches the context for the project Claude Code opened (matched by its git
# repository name, then its folder name) and Claude Code adds it to the model's context before the first turn.
# It prints nothing if NotDory is unreachable, so it never blocks a session. Other settings and hooks are kept;
# the file is backed up to settings.json.bak (same permissions) before each change. The access key and REST
# URL are kept in ~/.notdory/session-hook.curlrc, readable only by you; the hook command itself holds no credential.
# install-claude already installs this hook; use this script to manage the hook on its own.
# WSL 1 version: edits the settings of Claude Code inside this distro; the hook runs curl and git inside
# WSL at session start. For Claude Code on Windows, use scripts\windows\notdory-claude-hook.bat.
# Usage: scripts/wsl/notdory-claude-hook.sh install [REST_URL] [ACCESS_KEY]
#        scripts/wsl/notdory-claude-hook.sh remove
#   REST_URL     defaults to NOTDORY_REST_URL, else the NOTDORY_MCP_URL host on port 8700 (http://127.0.0.1:8700)
#   ACCESS_KEY   defaults to NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
# Requires python3, and curl for install.
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
ACTION="${1:-install}"
case "$ACTION" in
    -h|--help) usage; exit 0 ;;
    install|remove) ;;
    -?*) die "Unknown option: $ACTION (see --help)" ;;
    *) die "Unknown action: $ACTION (use install or remove; see --help)" ;;
esac
for arg in "${@:2}"; do
    case "$arg" in -?*) die "Unknown option: $arg (see --help)" ;; esac
done
if [ "$ACTION" = install ]; then
    [ "$#" -le 3 ] || die "Unexpected argument: $4 (usage: notdory-claude-hook.sh install [REST_URL] [ACCESS_KEY])"
else
    [ "$#" -le 1 ] || die "Unexpected argument: $2 (usage: notdory-claude-hook.sh remove)"
fi
wsl_require_cmd python3 "Install Python 3 inside WSL (for example: sudo apt install python3)."
SETTINGS="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/settings.json"
KEYFILE="$HOME/.notdory/session-hook.curlrc"
REST=""
TOKEN=""
if [ "$ACTION" = install ]; then
    wsl_require_cmd curl "Install curl inside WSL (for example: sudo apt install curl)."
    command -v git >/dev/null 2>&1 || echo "Note: git was not found; the hook will match projects by folder name only." >&2
    TOKEN="${3:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
    if [ -n "${NOTDORY_MCP_URL:-}" ]; then
        URL="$NOTDORY_MCP_URL"
    else
        URL="$(wsl_resolve_local_url "http://127.0.0.1:8720/mcp")"
    fi
# Checked before anything changes.
REST="$(python3 - "${2:-${NOTDORY_REST_URL:-}}" "$URL" "$TOKEN" <<'PY'
import re, sys
rest, mcp_url, key = sys.argv[1:4]
def fail(msg):
    print("Error: " + msg, file=sys.stderr)
    sys.exit(1)
if not key or any(ord(c) < 32 or ord(c) == 127 for c in key):
    fail("The access key is empty or contains control characters.")
if not rest:
    # The REST API is on the MCP host at port 8700 unless told otherwise.
    m = re.match(r"(https?)://(?:[^/?#@]*@)?(\[[^\]]*\]|[^/?#:]+)", mcp_url)
    if not m:
        fail("'%s' is not an http(s) URL." % mcp_url)
    rest = "%s://%s:8700" % (m.group(1), m.group(2))
rest = rest.rstrip("/")
if not re.match(r"https?://[^/?#\s]+\S*$", rest):
    fail("'%s' is not an http(s) URL." % rest)
print(rest)
PY
)"
fi
notdory_hook() {
    python3 - "$1" "$SETTINGS" "${REST:-}" "${TOKEN:-}" "$KEYFILE" <<'PY'
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
import shutil
action, path, rest, key, keyfile = sys.argv[1:6]
COMMAND = 'D="${CLAUDE_PROJECT_DIR:-$PWD}"; curl -fsS -m 8 -G -K "$HOME/.notdory/session-hook.curlrc" --data-urlencode "remote=$(git -C "$D" remote get-url origin 2>/dev/null)" --data-urlencode "directory=$(basename "$D")" --data-urlencode "path=$D" --data "format=text" || true'
def is_notdory(group):
    hooks = group.get("hooks") if isinstance(group, dict) else None
    if not isinstance(hooks, list):
        return False
    for h in hooks:
        c = h.get("command") if isinstance(h, dict) else None
        if isinstance(c, str) and ("session-hook.curlrc" in c or ("/v1.0/api/session" in c and "x-access-key" in c)):
            return True
    return False
def curl_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'
cfg = load(path)
check_case(cfg, "hooks", "The top-level object", path)
hooks = cfg.get("hooks")
if hooks is None:
    hooks = {}
elif not isinstance(hooks, dict):
    fail("hooks in %s is not a JSON object; it was left unchanged." % path)
check_case(hooks, "SessionStart", "hooks", path)
groups = hooks.get("SessionStart")
if groups is None:
    groups = []
elif not isinstance(groups, list):
    fail("hooks.SessionStart in %s is not a JSON array; it was left unchanged." % path)
found = any(is_notdory(g) for g in groups)
groups = [g for g in groups if not is_notdory(g)]
messages, changed = [], False
if action in ("remove", "reinstall"):
    if found:
        changed = True
        messages.append("Removed the NotDory SessionStart hook from %s." % path)
    else:
        messages.append("No NotDory SessionStart hook in %s; %s." % (path, "nothing to remove" if action == "remove" else "continuing"))
if action in ("install", "reinstall"):
    groups.append({"hooks": [{"type": "command", "command": COMMAND, "timeout": 10}]})
    changed = True
    messages.append("%s the NotDory SessionStart hook in %s (session context from %s)." % (
        "Updated" if found and action == "install" else "Installed", path, rest))
if changed:
    if groups:
        hooks["SessionStart"] = groups
    else:
        hooks.pop("SessionStart", None)
    if hooks:
        cfg["hooks"] = hooks
    else:
        cfg.pop("hooks", None)
    if os.path.exists(path):
        # One backup of the file as it was, readable by the same people as the original.
        shutil.copyfile(path, path + ".bak")
        os.chmod(path + ".bak", mode_of(path, 0o600))
    save(path, cfg)
if action == "remove":
    if os.path.exists(keyfile):
        os.unlink(keyfile)
        messages.append("Removed %s." % keyfile)
else:
    os.makedirs(os.path.dirname(keyfile), mode=0o700, exist_ok=True)
    write_atomic(keyfile, "# NotDory SessionStart hook settings, written by scripts/*/install-claude and notdory-claude-hook. Keep this file private.\n"
                 "url = " + curl_str(rest + "/v1.0/api/session") + "\nheader = " + curl_str("x-access-key: " + key) + "\n", force_mode=0o600)
    messages.append("Saved the hook's access key in %s." % keyfile)
print("\n".join(messages))
PY
}

notdory_hook "$ACTION"
echo "Restart Claude Code to pick up the change."
