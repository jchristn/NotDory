#!/usr/bin/env sh
# Connect Codex to the NotDory MCP server by writing ~/.codex/config.toml inside WSL. Usage: install-codex.sh [ACCESS_KEY]
# WSL 1 counterpart of scripts/linux/install-codex.sh. Same config edit as Linux; WSL 1 shares localhost with Windows.
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

# This script only writes Codex's WSL-side config; it never runs codex. Say so when codex is missing here.
if ! command -v codex >/dev/null 2>&1; then
  if PATH="$WSL_ORIGINAL_PATH" command -v codex >/dev/null 2>&1 || PATH="$WSL_ORIGINAL_PATH" command -v codex.exe >/dev/null 2>&1; then
    wsl_log "Note: codex is only installed on the Windows side, and Windows Codex reads %USERPROFILE%\.codex\config.toml."
    wsl_log "      For Windows Codex, run scripts\\windows\\install-codex.bat from Windows instead."
  else
    wsl_log "Note: codex is not installed in this distro yet (npm i -g @openai/codex). Writing the config anyway."
  fi
fi

wsl_run "$SCRIPT_DIR/../linux/install-codex.sh" "$@"
