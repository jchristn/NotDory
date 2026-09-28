#!/usr/bin/env sh
# Connect Claude Code to Isis, for every project, in one step:
#   1. Registers the Isis MCP server at user scope (every project sees it).
#   2. Installs a SessionStart hook that loads the project's memory context before the model's first turn; the project is
#      matched by its git repository name, then its folder name, and a new repository gets its own scope.
# Authenticates with the credential ACCESS KEY only (sent as the x-access-key header). The secret key is
# never sent and never leaves your machine; the access key is a capability token, so use a least-privilege one.
# Usage: install-claude.sh [ACCESS_KEY]  (arg #1 overrides ISIS_ACCESS_KEY)
# Override defaults with ISIS_MCP_URL / ISIS_REST_URL / ISIS_ACCESS_KEY. ISIS_REST_URL defaults to the MCP host on
# port 8700. Set ISIS_SESSION_HOOK=0 to skip the hook.
set -e

URL="${ISIS_MCP_URL:-http://127.0.0.1:8720/mcp}"
AK="${1:-${ISIS_ACCESS_KEY:-isisdefaultkey}}"
# The REST API is on the MCP host at port 8700 unless told otherwise.
HOST="$(printf '%s' "$URL" | sed -E 's#^(https?://[^/:]+).*#\1#')"
REST="${ISIS_REST_URL:-$HOST:8700}"

if ! command -v claude >/dev/null 2>&1; then
  echo "Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/claude-code" >&2
  exit 1
fi

# Re-running replaces the user-scope entry instead of failing on a duplicate.
claude mcp remove --scope user isis >/dev/null 2>&1 || true
claude mcp add --scope user --transport http isis "$URL" --header "x-access-key: $AK"
echo "Added 'isis' MCP server to Claude Code for every project ($URL)."

if [ "${ISIS_SESSION_HOOK:-1}" != "0" ]; then
  sh "$(dirname "$0")/isis-claude-hook.sh" install "$REST" "$AK"
fi
echo "Restart Claude Code to pick it up."
