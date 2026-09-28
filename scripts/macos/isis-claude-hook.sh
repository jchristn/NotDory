#!/usr/bin/env sh
# Install or remove the Isis SessionStart hook in the Claude Code user settings (~/.claude/settings.json).
# The hook fetches the session context for the project Claude Code opened (matched by its git repository name, then its
# folder name) and Claude Code adds it to the model's context before the first turn. It prints nothing if Isis is
# unreachable, so it never blocks a session. Other settings and hooks are kept; the file is backed up to .bak.
# Usage: isis-claude-hook.sh install REST_URL ACCESS_KEY
#        isis-claude-hook.sh remove
# Needs python3 or node to edit the JSON; without either it prints the hook to add by hand.
set -e

ACTION="${1:-install}"
REST_URL="${2:-http://127.0.0.1:8700}"
AK="${3:-isisdefaultkey}"
SETTINGS="$HOME/.claude/settings.json"
mkdir -p "$HOME/.claude"
[ -f "$SETTINGS" ] && cp "$SETTINGS" "$SETTINGS.bak"

COMMAND='D="${CLAUDE_PROJECT_DIR:-$PWD}"; curl -fsS -m 8 -G -H "x-access-key: '"$AK"'" --data-urlencode "remote=$(git -C "$D" remote get-url origin 2>/dev/null)" --data-urlencode "directory=$(basename "$D")" --data "format=text" "'"${REST_URL%/}"'/v1.0/api/session" || true'
export ISIS_HOOK_ACTION="$ACTION" ISIS_HOOK_COMMAND="$COMMAND" ISIS_HOOK_SETTINGS="$SETTINGS"

if command -v python3 >/dev/null 2>&1; then
  python3 - <<'PY'
import json, os
path, action, command = os.environ['ISIS_HOOK_SETTINGS'], os.environ['ISIS_HOOK_ACTION'], os.environ['ISIS_HOOK_COMMAND']
root = {}
if os.path.exists(path) and open(path).read().strip():
    root = json.load(open(path))
hooks = root.get('hooks') or {}
def is_isis(group):
    return any('/v1.0/api/session' in (h.get('command') or '') and 'x-access-key' in (h.get('command') or '') for h in group.get('hooks', []))
groups = [g for g in hooks.get('SessionStart', []) if not is_isis(g)]
if action == 'install':
    groups.append({'hooks': [{'type': 'command', 'command': command, 'timeout': 10}]})
if groups: hooks['SessionStart'] = groups
else: hooks.pop('SessionStart', None)
if hooks: root['hooks'] = hooks
else: root.pop('hooks', None)
json.dump(root, open(path, 'w'), indent=2)
PY
elif command -v node >/dev/null 2>&1; then
  node -e '
const fs = require("fs"); const e = process.env; const path = e.ISIS_HOOK_SETTINGS;
let root = {}; if (fs.existsSync(path) && fs.readFileSync(path, "utf8").trim()) root = JSON.parse(fs.readFileSync(path, "utf8"));
const hooks = root.hooks || {};
const isIsis = g => (g.hooks || []).some(h => (h.command || "").includes("/v1.0/api/session") && (h.command || "").includes("x-access-key"));
const groups = (hooks.SessionStart || []).filter(g => !isIsis(g));
if (e.ISIS_HOOK_ACTION === "install") groups.push({ hooks: [{ type: "command", command: e.ISIS_HOOK_COMMAND, timeout: 10 }] });
if (groups.length) hooks.SessionStart = groups; else delete hooks.SessionStart;
if (Object.keys(hooks).length) root.hooks = hooks; else delete root.hooks;
fs.writeFileSync(path, JSON.stringify(root, null, 2));'
else
  echo "Neither python3 nor node is available to edit $SETTINGS. Add this SessionStart hook by hand:" >&2
  echo "  {\"hooks\":{\"SessionStart\":[{\"hooks\":[{\"type\":\"command\",\"timeout\":10,\"command\":<the command below>}]}]}}" >&2
  echo "  $COMMAND" >&2
  exit 1
fi

if [ "$ACTION" = "install" ]; then
  echo "Installed the Isis SessionStart hook in $SETTINGS (session context from $REST_URL)."
else
  echo "Removed the Isis SessionStart hook from $SETTINGS."
fi
