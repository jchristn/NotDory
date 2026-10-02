#!/usr/bin/env bash
# Run NotDory.Server from the working tree against the benchmark stack (benchmarks/docker/compose.yaml).
# REST on 127.0.0.1:18700, Prometheus metrics on 127.0.0.1:19464. Build first: dotnet build src/NotDory.sln -c Release
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$here/.run"
cd "$here/.run"
export NOTDORY_SETTINGS_FILE=notdory.bench.json
export NOTDORY_REST_PORT=18700 NOTDORY_REST_HOSTNAME=127.0.0.1
export NOTDORY_DB_TYPE=Postgresql NOTDORY_DB_SERVER=127.0.0.1 NOTDORY_DB_PORT=15432 NOTDORY_DB_DATABASE=notdory NOTDORY_DB_USERNAME=notdory NOTDORY_DB_PASSWORD=notdory
export NOTDORY_RECALLDB_ENDPOINT=http://127.0.0.1:18600 NOTDORY_RECALLDB_ADMIN_KEY=recalldbadmin
export NOTDORY_OBS_ENABLED=true NOTDORY_OBS_PROM_HOSTNAME=127.0.0.1 NOTDORY_OBS_PROM_PORT=19464
export NOTDORY_DEFAULT_ENDPOINT_BASEURL=${NOTDORY_DEFAULT_ENDPOINT_BASEURL:-http://127.0.0.1:11434}
exec dotnet run --project "$here/../src/NotDory.Server" -c Release --no-build
