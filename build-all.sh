#!/usr/bin/env bash
set -euo pipefail

if [ $# -lt 1 ] || [ -z "$1" ]; then
    echo "Usage: build-all.sh <tag>"
    echo "Example: build-all.sh v0.1.0"
    exit 1
fi

TAG="$1"

cd "$(dirname "$0")"

./build-server.sh "$TAG"
./build-dashboard.sh "$TAG"

echo "Done."
