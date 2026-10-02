#!/usr/bin/env sh
# Connect Claude Code to NotDory, for every project, in one step:
#   1. Registers the NotDory MCP server at user scope (every project sees it).
#   2. Installs a SessionStart hook that loads the project's memory context before the model's first turn; the project is
#      matched by its git repository name, then its folder name, and a new repository gets its own scope.
# Authenticates with the credential ACCESS KEY only (sent as the x-access-key header). The secret key is
# never sent and never leaves your machine; the access key is a capability token, so use a least-privilege one.
# Usage: install-claude.sh [ACCESS_KEY]  (arg #1 overrides NOTDORY_ACCESS_KEY)
# Override defaults with NOTDORY_MCP_URL / NOTDORY_REST_URL / NOTDORY_ACCESS_KEY. NOTDORY_REST_URL defaults to the MCP host on
# port 8700. Set NOTDORY_SESSION_HOOK=0 to skip the hook.
set -e

URL="${NOTDORY_MCP_URL:-http://127.0.0.1:8720/mcp}"
AK="${1:-${NOTDORY_ACCESS_KEY:-notdorydefaultkey}}"
# The REST API is on the MCP host at port 8700 unless told otherwise.
HOST="$(printf '%s' "$URL" | sed -E 's#^(https?://[^/:]+).*#\1#')"
REST="${NOTDORY_REST_URL:-$HOST:8700}"

if ! command -v claude >/dev/null 2>&1; then
  echo "Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/claude-code" >&2
  exit 1
fi

# Re-running replaces the user-scope entry instead of failing on a duplicate.
claude mcp remove --scope user notdory >/dev/null 2>&1 || true
claude mcp add --scope user --transport http notdory "$URL" --header "x-access-key: $AK"
echo "Added 'notdory' MCP server to Claude Code for every project ($URL)."

if [ "${NOTDORY_SESSION_HOOK:-1}" != "0" ]; then
  sh "$(dirname "$0")/notdory-claude-hook.sh" install "$REST" "$AK"
fi
echo "Restart Claude Code to pick it up."
