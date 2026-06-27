#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${REPO_ROOT}"

log() {
  echo "[factory-reset] $1"
}

log "Factory reset will remove Docker data, named volumes, and regenerated Wilson settings."
printf "Type RESET to continue: "
read -r CONFIRMATION

if [[ "${CONFIRMATION}" != "RESET" ]]; then
  log "Confirmation did not match RESET. Aborting."
  exit 1
fi

if command -v docker >/dev/null 2>&1; then
  log "Stopping Docker Compose services and removing anonymous volumes."
  docker compose -f docker/compose.yaml down --volumes --remove-orphans >/dev/null 2>&1 || true
else
  log "Docker is not installed or not on PATH. Skipping docker compose shutdown."
fi

log "Removing persisted Docker data."
rm -rf docker/data

log "Recreating required directory structure."
mkdir -p docker/data

log "Restoring factory-default Docker settings files."
cp docker/factory/wilson.json docker/wilson.json

log "Prompt template defaults will be recreated by Wilson on next server startup."
log "Factory reset completed."
