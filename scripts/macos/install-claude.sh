#!/usr/bin/env bash
# Connect Claude Code to the NotDory MCP server at user scope (every project sees it), via the claude CLI.
# Also installs the NotDory SessionStart hook in ${CLAUDE_CONFIG_DIR:-~/.claude}/settings.json, which loads the
# project's memory context before the first turn (NOTDORY_SESSION_HOOK=0 removes it instead).
# Idempotent: an existing 'notdory' entry is replaced; every other server is kept.
# Authenticates with the credential ACCESS KEY only: Claude Code, Codex, Cursor and the Gemini CLI
# send it in the x-access-key header; Mux sends it as a bearer token (Authorization: Bearer <key>),
# as docs/INSTRUCTIONS_FOR_MUX.md describes. The secret key is never sent. The access key is a
# capability token, so use a least-privilege credential.
# The hook reads the access key and REST URL from ~/.notdory/session-hook.curlrc (readable only by you),
# so settings.json holds no credential.
# Usage: scripts/macos/install-claude.sh [ACCESS_KEY]
#   ACCESS_KEY        overrides NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
#   NOTDORY_MCP_URL   server URL (default: http://127.0.0.1:8720/mcp)
#   NOTDORY_REST_URL  REST API for the hook (default: the MCP host on port 8700)
# Requires the claude CLI, python3 and curl (the hook runs it).
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Darwin ]; then
    die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 or scripts/wsl under WSL, or scripts\\windows on Windows."
fi

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -le 1 ] || die "Unexpected argument: $2 (usage: install-claude.sh [ACCESS_KEY])"
TOKEN="${1:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
command -v claude >/dev/null 2>&1 || die "'claude' was not found. Install Claude Code: curl -fsSL https://claude.ai/install.sh | bash (see https://code.claude.com/docs/en/setup)."
command -v python3 >/dev/null 2>&1 || die "'python3' was not found. Install Python 3 with xcode-select --install or from https://www.python.org/downloads/macos/."
SETTINGS="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/settings.json"
KEYFILE="$HOME/.notdory/session-hook.curlrc"
# The same rule as the Windows scripts: http:// or https://, a host, and no whitespace.
URL_RE='^https?://[^/?#[:space:]]+[^[:space:]]*$'
check_url() { [[ "$1" =~ $URL_RE ]] || die "'$1' is not an http(s) URL."; }
URL="${NOTDORY_MCP_URL:-http://127.0.0.1:8720/mcp}"
check_url "$URL"
HOOK="${NOTDORY_SESSION_HOOK:-1}"
REST=""
if [ "$HOOK" != 0 ]; then
    command -v curl >/dev/null 2>&1 || die "'curl' was not found. curl ships with macOS; check your PATH."
    command -v git >/dev/null 2>&1 || echo "Note: git was not found; the hook will match projects by folder name only." >&2
# Checked before anything changes.
REST="$(python3 - "${NOTDORY_REST_URL:-}" "$URL" "$TOKEN" <<'PY'
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

# Prints "removed" or "absent"; fails on any other error from the claude CLI.
claude_remove() {
    local out
    if out="$(claude mcp remove --scope user notdory 2>&1)"; then echo removed; return 0; fi
    case "$out" in
        *"No MCP server named"*) echo absent ;;
        *) die "claude mcp remove failed: $out" ;;
    esac
}
claude_add() {
    claude mcp add --scope user --transport http notdory "$URL" --header "x-access-key: $TOKEN" >/dev/null || die "claude mcp add failed."
}
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

STATUS="$(claude_remove)"
claude_add
if [ "$STATUS" = removed ]; then
    echo "Replaced the existing 'notdory' entry in Claude Code at user scope ($URL)."
else
    echo "Added 'notdory' to Claude Code at user scope ($URL)."
fi
if [ "$HOOK" != 0 ]; then
    notdory_hook install
else
    # Disabled: drop an older hook (and its key) rather than leave it behind.
    notdory_hook remove
    echo "Skipped installing the SessionStart hook (NOTDORY_SESSION_HOOK=0)."
fi
echo "Restart Claude Code to pick up the change."
