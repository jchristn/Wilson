#!/usr/bin/env bash
set -euo pipefail

if [ $# -lt 1 ] || [ -z "$1" ]; then
    echo "Usage: build-dashboard.sh <tag>"
    echo "Example: build-dashboard.sh v0.1.0"
    exit 1
fi

TAG="$1"
IMAGE="jchristn77/wilson-dashboard"

cd "$(dirname "$0")"

echo "Building ${IMAGE}:latest and ${IMAGE}:${TAG}..."
docker buildx build \
    --builder cloud-jchristn77-jchristn77 \
    --platform linux/amd64,linux/arm64/v8 \
    -t "${IMAGE}:latest" \
    -t "${IMAGE}:${TAG}" \
    -f docker/dashboard/Dockerfile \
    --push \
    .

echo "Updating local images for ${IMAGE}:latest and ${IMAGE}:${TAG}..."
docker pull "${IMAGE}:latest"
docker pull "${IMAGE}:${TAG}"

echo "Done."
