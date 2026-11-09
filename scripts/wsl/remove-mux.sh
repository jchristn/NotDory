#!/usr/bin/env sh
# Disconnect Mux from the NotDory MCP server by editing ~/.mux/mcp-servers.json inside WSL.
# WSL 1 counterpart of scripts/linux/remove-mux.sh. Same config edit as Linux; the Windows-side config is left alone (use scripts\windows for that).
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

if [ -z "${NOTDORY_MUX_CONFIG:-}" ] && [ -n "${MUX_CONFIG_DIR:-}" ]; then
  NOTDORY_MUX_CONFIG="$MUX_CONFIG_DIR/mcp-servers.json"
  export NOTDORY_MUX_CONFIG
fi

wsl_run "$SCRIPT_DIR/../linux/remove-mux.sh" "$@"
