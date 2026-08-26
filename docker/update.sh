#!/usr/bin/env bash
# Wilson stack update entry point. Tears the stack down, pulls the latest published
# images, brings it back up detached, then lists all containers. Paths are anchored to
# this script's directory so Compose always uses docker/compose.yaml.
set -euo pipefail
cd "$(dirname "$0")"

echo "[Wilson] Stopping the stack..."
docker compose down --remove-orphans

# Compose uses fixed container_name values, so a leftover container (from a crash or a
# concurrent compose run) can survive `down` and collide on `up` with
# "container name is already in use". Remove any such leftovers by name first.
echo "[Wilson] Clearing any leftover fixed-name containers..."
for name in wilson-server wilson-dashboard; do
    docker rm -f "$name" >/dev/null 2>&1 || true
done

echo "[Wilson] Pulling images..."
docker compose pull

echo "[Wilson] Starting the stack (detached)..."
docker compose up -d --remove-orphans

echo "[Wilson] Containers:"
docker ps -a
