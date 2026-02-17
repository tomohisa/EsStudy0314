#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
login_if_needed
set_subscription_if_configured

DEPLOYMENT_NAME="aca-infra-${ENVIRONMENT}"
TMP_FILE="${OUTPUTS_FILE}.tmp"

az_retry deployment group create \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "${SCRIPT_DIR}/main.bicep" \
  --parameters \
    logRetentionInDays="$LOG_RETENTION_IN_DAYS" \
    logDailyQuotaGb="$LOG_DAILY_QUOTA_GB" \
    backendMinReplicas="$BACKEND_MIN_REPLICAS" \
    backendMaxReplicas="$BACKEND_MAX_REPLICAS" \
    frontendMinReplicas="$FRONTEND_MIN_REPLICAS" \
    frontendMaxReplicas="$FRONTEND_MAX_REPLICAS" \
    adminwebMinReplicas="$ADMINWEB_MIN_REPLICAS" \
    adminwebMaxReplicas="$ADMINWEB_MAX_REPLICAS" \
  --output json > "$TMP_FILE"

jq '.properties.outputs | with_entries(.value = .value.value)' "$TMP_FILE" > "$OUTPUTS_FILE"
rm -f "$TMP_FILE"

APP_INSIGHTS_CONNECTION_STRING="$(jq -r '.applicationInsightsConnectionString // empty' "$OUTPUTS_FILE")"
MANAGED_ENVIRONMENT_NAME="$(jq -r '.managedEnvironmentName // empty' "$OUTPUTS_FILE")"
if [ -n "$APP_INSIGHTS_CONNECTION_STRING" ] && [ -n "$MANAGED_ENVIRONMENT_NAME" ]; then
  az_retry containerapp env telemetry app-insights set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$MANAGED_ENVIRONMENT_NAME" \
    --connection-string "$APP_INSIGHTS_CONNECTION_STRING" \
    --enable-open-telemetry-traces true \
    --enable-open-telemetry-logs true >/dev/null
fi

BACKEND_APP_NAME="$(jq -r '.backendAppName // empty' "$OUTPUTS_FILE")"
FRONTEND_APP_NAME="$(jq -r '.frontendAppName // empty' "$OUTPUTS_FILE")"
ADMINWEB_APP_NAME="$(jq -r '.adminwebAppName // empty' "$OUTPUTS_FILE")"

if [ -n "$BACKEND_APP_NAME" ]; then
  wait_for_containerapp_ready "$BACKEND_APP_NAME"
fi
if [ -n "$FRONTEND_APP_NAME" ]; then
  wait_for_containerapp_ready "$FRONTEND_APP_NAME"
fi
if [ -n "$ADMINWEB_APP_NAME" ]; then
  wait_for_containerapp_ready "$ADMINWEB_APP_NAME"
fi

echo "Infrastructure deployed."
echo "Outputs saved to: $OUTPUTS_FILE"
jq '.' "$OUTPUTS_FILE"
