#!/usr/bin/env sh
# Connect Cursor to the NotDory MCP server by writing ~/.cursor/mcp.json inside WSL. Usage: install-cursor.sh [ACCESS_KEY]
# WSL 2 counterpart of scripts/linux/install-cursor.sh. Same config edit as Linux; on WSL 2 the default 127.0.0.1 endpoint falls back to the Windows host.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd python3 "Install it inside WSL (for example: sudo apt install python3)."

# Under WSL 2 NAT networking, 127.0.0.1 is the distro, not Windows; if the server only answers on the
# Windows host, use that address. An explicit NOTDORY_MCP_URL is used as given.
if [ -z "${NOTDORY_MCP_URL:-}" ]; then
  NOTDORY_MCP_URL="$(wsl_resolve_local_url "http://127.0.0.1:8720/mcp")"
  export NOTDORY_MCP_URL
fi

wsl_log "This writes the WSL-side ~/.cursor/mcp.json, which Cursor reads when it runs inside WSL (or via its WSL remote)."
wsl_log "For Cursor on Windows, run scripts\\windows\\install-cursor.bat from Windows to write %USERPROFILE%\.cursor\mcp.json."

wsl_run "$SCRIPT_DIR/../linux/install-cursor.sh" "$@"
