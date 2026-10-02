#!/usr/bin/env bash
# Builds the backend (NotDory.sln) and the dashboard locally.
set -euo pipefail
cd "$(dirname "$0")"

echo "=== Backend build (NotDory.sln) ==="
dotnet build src/NotDory.sln -c Release

echo "=== Dashboard build ==="
(cd dashboard && npm ci && npm run build)

echo "Build complete."
