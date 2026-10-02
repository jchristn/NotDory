#!/usr/bin/env bash
# Builds and pushes the server, MCP, and dashboard images with the given tag.
set -euo pipefail

if [ $# -lt 1 ] || [ -z "$1" ]; then
    echo "Usage: build-all.sh <tag>"
    echo "Example: build-all.sh v0.1.0"
    exit 1
fi

TAG="$1"
DIR="$(cd "$(dirname "$0")" && pwd)"

"$DIR/build-server.sh" "$TAG"
"$DIR/build-mcp.sh" "$TAG"
"$DIR/build-dashboard.sh" "$TAG"

echo "Done."
