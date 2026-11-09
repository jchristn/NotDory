#!/usr/bin/env sh
# Connect Mux to the NotDory MCP server by writing ~/.mux/mcp-servers.json inside WSL. Usage: install-mux.sh [ACCESS_KEY]
# WSL 1 counterpart of scripts/linux/install-mux.sh. Same config edit as Linux; WSL 1 shares localhost with Windows.
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

# This script only writes Mux's WSL-side config; it never runs mux. Say so when mux is missing here.
if ! command -v mux >/dev/null 2>&1; then
  if PATH="$WSL_ORIGINAL_PATH" command -v mux >/dev/null 2>&1 || PATH="$WSL_ORIGINAL_PATH" command -v mux.exe >/dev/null 2>&1; then
    wsl_log "Note: mux is only installed on the Windows side, and Windows Mux reads %USERPROFILE%\.mux\mcp-servers.json."
    wsl_log "      For Windows Mux, run scripts\\windows\\install-mux.bat from Windows instead."
  else
    wsl_log "Note: mux is not installed in this distro yet (install it with the Mux repo's install-tool.sh for WSL, or dotnet tool install -g Mux.Cli). Writing the config anyway."
  fi
fi

# Mux reads mcp-servers.json from $MUX_CONFIG_DIR when that is set, otherwise from ~/.mux.
if [ -z "${NOTDORY_MUX_CONFIG:-}" ] && [ -n "${MUX_CONFIG_DIR:-}" ]; then
  NOTDORY_MUX_CONFIG="$MUX_CONFIG_DIR/mcp-servers.json"
  export NOTDORY_MUX_CONFIG
fi
if command -v mux >/dev/null 2>&1; then
  case "$(command -v mux)" in
    "$HOME/.dotnet/tools/"*) ;;
    *) wsl_log "Note: using mux at $(command -v mux) (not the dotnet global tool in ~/.dotnet/tools)." ;;
  esac
fi

wsl_run "$SCRIPT_DIR/../linux/install-mux.sh" "$@"
