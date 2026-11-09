#!/usr/bin/env sh
# Connect the Gemini CLI to the NotDory MCP server by writing ~/.gemini/settings.json inside WSL. Usage: install-gemini.sh [ACCESS_KEY]
# WSL 2 counterpart of scripts/linux/install-gemini.sh. Same config edit as Linux; on WSL 2 the default 127.0.0.1 endpoint falls back to the Windows host.
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

# This script only writes the Gemini CLI's WSL-side config; it never runs gemini. Say so when gemini is missing here.
if ! command -v gemini >/dev/null 2>&1; then
  if PATH="$WSL_ORIGINAL_PATH" command -v gemini >/dev/null 2>&1 || PATH="$WSL_ORIGINAL_PATH" command -v gemini.exe >/dev/null 2>&1; then
    wsl_log "Note: gemini is only installed on the Windows side, and Windows the Gemini CLI reads %USERPROFILE%\.gemini\settings.json."
    wsl_log "      For Windows the Gemini CLI, run scripts\\windows\\install-gemini.bat from Windows instead."
  else
    wsl_log "Note: gemini is not installed in this distro yet (npm i -g @google/gemini-cli). Writing the config anyway."
  fi
fi

wsl_run "$SCRIPT_DIR/../linux/install-gemini.sh" "$@"
