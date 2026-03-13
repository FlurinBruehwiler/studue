#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="${WORK_DIR:-$PWD}"
FRONTEND_ARTIFACT_DIR="${FRONTEND_ARTIFACT_DIR:-$WORK_DIR/frontend-dist}"
BACKEND_ARTIFACT_DIR="${BACKEND_ARTIFACT_DIR:-$WORK_DIR/backend-build}"
WWW_ROOT="${WWW_ROOT:-/var/www/studue}"
RELEASES_DIR="${RELEASES_DIR:-/opt/studue/releases}"
CURRENT_LINK="${CURRENT_LINK:-/opt/studue/current}"
DATA_DIR="${DATA_DIR:-/var/lib/studue/data}"
BACKEND_VERSION="${BACKEND_VERSION:-}"

log() {
  printf '[deploy] %s\n' "$1"
}

fail() {
  printf '[deploy] ERROR: %s\n' "$1" >&2
  exit 1
}

require_dir() {
  local path="$1"
  [[ -d "$path" ]] || fail "Directory not found: $path"
}

require_file() {
  local path="$1"
  [[ -f "$path" ]] || fail "File not found: $path"
}

find_backend_archive() {
  local pattern

  if [[ -n "$BACKEND_VERSION" ]]; then
    pattern="$BACKEND_ARTIFACT_DIR/distributions/backend-$BACKEND_VERSION.tar"
    require_file "$pattern"
    printf '%s\n' "$pattern"
    return
  fi

  local matches=()
  while IFS= read -r line; do
    matches+=("$line")
  done < <(compgen -G "$BACKEND_ARTIFACT_DIR/distributions/backend-*.tar" || true)

  [[ ${#matches[@]} -gt 0 ]] || fail "No backend distribution archive found in $BACKEND_ARTIFACT_DIR/distributions"
  printf '%s\n' "${matches[0]}"
}

main() {
  require_dir "$FRONTEND_ARTIFACT_DIR"
  require_dir "$BACKEND_ARTIFACT_DIR"
  require_dir "$BACKEND_ARTIFACT_DIR/distributions"

  local backend_archive
  backend_archive="$(find_backend_archive)"

  local backend_basename
  backend_basename="$(basename "$backend_archive" .tar)"

  mkdir -p "$WWW_ROOT" "$RELEASES_DIR" "$DATA_DIR"

  log "Syncing frontend files to $WWW_ROOT"
  rsync -av --delete "$FRONTEND_ARTIFACT_DIR/" "$WWW_ROOT/"

  log "Extracting backend archive $backend_archive"
  tar -xf "$backend_archive" -C "$RELEASES_DIR"

  log "Updating current backend symlink to $backend_basename"
  ln -sfn "$RELEASES_DIR/$backend_basename" "$CURRENT_LINK"

  if [[ ! -f "$DATA_DIR/access-control.json" ]]; then
    log "No access-control.json found in $DATA_DIR; backend will create a default one on first start"
  fi

  log "Deployment files are in place"
  log "If needed, restart the backend: sudo systemctl restart studue"
  log "If needed, reload caddy: sudo systemctl reload caddy"
}

main "$@"
