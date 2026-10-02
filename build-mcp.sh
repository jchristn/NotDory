#!/usr/bin/env bash
# Builds and pushes the multi-arch jchristn77/notdory-mcp image, tagged latest and <tag>, then pulls both locally.
set -euo pipefail

if [ $# -lt 1 ] || [ -z "$1" ]; then
    echo "Usage: build-mcp.sh <tag>"
    echo "Example: build-mcp.sh v0.1.0"
    exit 1
fi

TAG="$1"
IMAGE=jchristn77/notdory-mcp

cd "$(dirname "$0")"

echo "Building $IMAGE:latest and $IMAGE:$TAG..."
docker buildx build \
    --builder cloud-jchristn77-jchristn77 \
    --platform linux/amd64,linux/arm64/v8 \
    -t "$IMAGE:latest" \
    -t "$IMAGE:$TAG" \
    -f docker/mcp/Dockerfile \
    --push \
    .

echo "Pulling $IMAGE:$TAG and $IMAGE:latest into local registry..."
docker pull "$IMAGE:$TAG"
docker pull "$IMAGE:latest"

echo "Done."
