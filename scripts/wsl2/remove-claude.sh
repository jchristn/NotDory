#!/usr/bin/env sh
# Disconnect Claude Code (installed inside WSL) from NotDory: removes the user-scope MCP server and the SessionStart hook.
# WSL 2 counterpart of scripts/linux/remove-claude.sh. Uses the Linux claude CLI and scripts/wsl2/notdory-claude-hook.sh.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd claude "Install Claude Code inside WSL: npm i -g @anthropic-ai/claude-code"

claude mcp remove --scope user notdory || true
sh "$SCRIPT_DIR/notdory-claude-hook.sh" remove
echo "Removed 'notdory' MCP server and the SessionStart hook from Claude Code (inside WSL)."
