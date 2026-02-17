#!/bin/bash
set -euo pipefail

if [ -z "${1:-}" ] || [ -z "${2:-}" ]; then
  echo "Usage: $0 <environment-name> <path-to-bicep-file>"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "$1"
login_if_needed
set_subscription_if_configured
BICEP_FILE="$2"

if [ ! -f "$SCRIPT_DIR/$BICEP_FILE" ] && [ ! -f "$BICEP_FILE" ]; then
  echo "Error: bicep file not found: $BICEP_FILE"
  exit 1
fi

TEMPLATE_PATH="$BICEP_FILE"
if [ -f "$SCRIPT_DIR/$BICEP_FILE" ]; then
  TEMPLATE_PATH="$SCRIPT_DIR/$BICEP_FILE"
fi

az_retry deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$TEMPLATE_PATH" \
  --parameters \
    logRetentionInDays="$LOG_RETENTION_IN_DAYS" \
    logDailyQuotaGb="$LOG_DAILY_QUOTA_GB" \
    backendMinReplicas="$BACKEND_MIN_REPLICAS" \
    backendMaxReplicas="$BACKEND_MAX_REPLICAS" \
    frontendMinReplicas="$FRONTEND_MIN_REPLICAS" \
    frontendMaxReplicas="$FRONTEND_MAX_REPLICAS" \
    adminwebMinReplicas="$ADMINWEB_MIN_REPLICAS" \
    adminwebMaxReplicas="$ADMINWEB_MAX_REPLICAS"
