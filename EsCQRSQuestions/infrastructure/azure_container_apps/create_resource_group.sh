#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
login_if_needed
set_subscription_if_configured

echo "Create resource group: ${RESOURCE_GROUP} in location: ${LOCATION}"
az_retry group create --name "$RESOURCE_GROUP" --location "$LOCATION" >/dev/null

echo "Resource group ready: $RESOURCE_GROUP"
