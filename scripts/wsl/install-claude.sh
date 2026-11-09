#!/usr/bin/env sh
# Connect Claude Code (installed inside WSL) to NotDory for every project: registers the MCP server at user scope and installs the SessionStart hook. Usage: install-claude.sh [ACCESS_KEY]
# WSL 1 counterpart of scripts/linux/install-claude.sh. Uses the Linux claude CLI (never the Windows shim) and installs the hook through scripts/wsl/notdory-claude-hook.sh into the WSL-side ~/.claude/settings.json.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd claude "Install Claude Code inside WSL: npm i -g @anthropic-ai/claude-code"

# WSL 1 shares localhost with Windows, so the default 127.0.0.1 endpoint reaches a server on either side.

URL="${NOTDORY_MCP_URL:-http://127.0.0.1:8720/mcp}"
AK="${1:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
# The REST API is on the MCP host at port 8700 unless told otherwise.
HOST="$(printf '%s' "$URL" | sed -E 's#^(https?://[^/:]+).*#\1#')"
REST="${NOTDORY_REST_URL:-$HOST:8700}"

# Re-running replaces the user-scope entry instead of failing on a duplicate.
claude mcp remove --scope user notdory >/dev/null 2>&1 || true
claude mcp add --scope user --transport http notdory "$URL" --header "x-access-key: $AK"
echo "Added 'notdory' MCP server to Claude Code (inside WSL) for every project ($URL)."

if [ "${NOTDORY_SESSION_HOOK:-1}" != "0" ]; then
  sh "$SCRIPT_DIR/notdory-claude-hook.sh" install "$REST" "$AK"
fi
printf '%s\n' "Restart Claude Code to pick it up. For Claude Code on Windows, run scripts\\windows\\install-claude.bat from Windows."
