#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"

DEPLOYMENT_NAME="aca-infra-${ENVIRONMENT}-$(date +%Y%m%d%H%M%S)"
TMP_FILE="${OUTPUTS_FILE}.tmp"

az deployment group create \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "${SCRIPT_DIR}/main.bicep" \
  --output json > "$TMP_FILE"

jq '.properties.outputs | with_entries(.value = .value.value)' "$TMP_FILE" > "$OUTPUTS_FILE"
rm -f "$TMP_FILE"

echo "Infrastructure deployed."
echo "Outputs saved to: $OUTPUTS_FILE"
jq '.' "$OUTPUTS_FILE"
