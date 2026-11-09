#!/usr/bin/env sh
# Connect the Gemini CLI to the NotDory MCP server by writing ~/.gemini/settings.json inside WSL. Usage: install-gemini.sh [ACCESS_KEY]
# WSL 1 counterpart of scripts/linux/install-gemini.sh. Same config edit as Linux; WSL 1 shares localhost with Windows.
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
