#!/usr/bin/env sh
# Connect Cursor to the NotDory MCP server by writing ~/.cursor/mcp.json inside WSL. Usage: install-cursor.sh [ACCESS_KEY]
# WSL 1 counterpart of scripts/linux/install-cursor.sh. Same config edit as Linux; WSL 1 shares localhost with Windows.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd python3 "Install it inside WSL (for example: sudo apt install python3)."

# WSL 1 shares localhost with Windows, so the default 127.0.0.1 endpoint reaches a server on either side.

wsl_log "This writes the WSL-side ~/.cursor/mcp.json, which Cursor reads when it runs inside WSL (or via its WSL remote)."
wsl_log "For Cursor on Windows, run scripts\\windows\\install-cursor.bat from Windows to write %USERPROFILE%\.cursor\mcp.json."

wsl_run "$SCRIPT_DIR/../linux/install-cursor.sh" "$@"
