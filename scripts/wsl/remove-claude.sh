#!/usr/bin/env bash
# Disconnect Claude Code from the NotDory MCP server (user scope), via the claude CLI.
# Also removes the NotDory SessionStart hook from ${CLAUDE_CONFIG_DIR:-~/.claude}/settings.json and its key file.
# Succeeds with "nothing to remove" when there is no 'notdory' entry; every other server is kept.
# WSL 1 version: configures Claude Code inside this distro (not on Windows). For Claude Code on Windows, run
# scripts\windows\remove-claude.bat from Windows.
# Usage: scripts/wsl/remove-claude.sh
# Requires the claude CLI and python3.
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
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments; see --help)"
wsl_require_cmd claude "Install Claude Code inside WSL: curl -fsSL https://claude.ai/install.sh | bash (see https://code.claude.com/docs/en/setup)."
wsl_require_cmd python3 "Install Python 3 inside WSL (for example: sudo apt install python3)."
SETTINGS="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/settings.json"
KEYFILE="$HOME/.notdory/session-hook.curlrc"
wsl_log "This configures Claude Code inside this WSL distro. For Claude Code on Windows, run scripts\\windows\\remove-claude.bat from Windows."

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

# Assigned first: a failure inside $(...) in an if test would not stop the script.
STATUS="$(claude_remove)"
if [ "$STATUS" = removed ]; then
    echo "Removed 'notdory' from Claude Code at user scope."
else
    echo "No 'notdory' entry in Claude Code at user scope; nothing to remove."
fi
notdory_hook remove
echo "Restart Claude Code to pick up the change."
