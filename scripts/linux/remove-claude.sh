#!/usr/bin/env sh
# Disconnect Claude Code from NotDory: removes the user-scope MCP server and the SessionStart hook.
set -e

if ! command -v claude >/dev/null 2>&1; then
  echo "Claude CLI not found on PATH." >&2
  exit 1
fi

claude mcp remove --scope user notdory || true
sh "$(dirname "$0")/notdory-claude-hook.sh" remove
echo "Removed 'notdory' MCP server and the SessionStart hook from Claude Code."
