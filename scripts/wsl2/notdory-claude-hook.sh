#!/usr/bin/env sh
# Install or remove the NotDory SessionStart hook in the WSL-side Claude Code user settings (~/.claude/settings.json). Usage: notdory-claude-hook.sh install [REST_URL] [ACCESS_KEY] | remove
# WSL 2 counterpart of scripts/linux/notdory-claude-hook.sh. The hook runs curl and git inside WSL at session start, so both must be Linux installs.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

ACTION="${1:-install}"
if [ "$ACTION" = "install" ]; then
  wsl_require_cmd curl "Install it inside WSL (for example: sudo apt install curl)."
  command -v git >/dev/null 2>&1 || wsl_log "Note: git is not installed in this distro; the hook will match projects by folder name only."
  if [ -n "${2:-}" ]; then
    REST="$2"
  else
    REST="$(wsl_resolve_local_url "http://127.0.0.1:8700")"
  fi
  wsl_run "$SCRIPT_DIR/../linux/notdory-claude-hook.sh" install "$REST" "${3:-notdorydefaultkey}"
fi
wsl_run "$SCRIPT_DIR/../linux/notdory-claude-hook.sh" "$@"
