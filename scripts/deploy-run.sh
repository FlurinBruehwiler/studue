#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <run-id>" >&2
  exit 1
fi

RUN_ID="$1"
REPO="FlurinBruehwiler/studue"

gh run download "$RUN_ID" -R "$REPO"
chmod +x deploy-script/deploy-artifacts.sh
sudo ./deploy-script/deploy-artifacts.sh
sudo systemctl restart studue
