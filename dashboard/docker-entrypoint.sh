#!/bin/sh
set -eu

# Emit a small config.js consumed by index.html before the app bundle loads.
# Env vars are optional; unset ones leave the field out so the SPA falls back
# to its build-time defaults from constants.js.
config_path=/usr/share/nginx/html/config.js

{
  printf 'window.__NOTDORY_CONFIG__ = {'
  first=1
  emit() {
    key=$1
    val=$2
    if [ -n "$val" ]; then
      if [ $first -eq 0 ]; then printf ','; fi
      # JSON-escape backslashes and double quotes.
      esc=$(printf '%s' "$val" | sed 's/\\/\\\\/g; s/"/\\"/g')
      printf '"%s":"%s"' "$key" "$esc"
      first=0
    fi
  }
  emit serverUrl   "${NOTDORY_SERVER_URL:-}"
  emit adminEmail  "${NOTDORY_ADMIN_EMAIL:-}"
  emit tenantId    "${NOTDORY_TENANT_ID:-}"
  printf '};\n'
} > "$config_path"

exec "$@"
