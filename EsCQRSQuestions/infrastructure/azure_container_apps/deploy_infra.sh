#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
login_if_needed
set_subscription_if_configured

DEPLOYMENT_NAME="aca-infra-${ENVIRONMENT}"
TMP_FILE="${OUTPUTS_FILE}.tmp"

enforce_admin_easyauth_user_lock() {
  if [ "${ADMIN_EASYAUTH_ENABLED}" != "true" ] || [ -z "${ADMIN_EASYAUTH_CLIENT_ID}" ]; then
    return
  fi

  local allowed_user_id="${ADMIN_EASYAUTH_ALLOWED_USER_OBJECT_ID}"
  if [ -z "${allowed_user_id}" ]; then
    allowed_user_id="$(az ad signed-in-user show --query id -o tsv 2>/dev/null || true)"
  fi
  if [ -z "${allowed_user_id}" ]; then
    echo "Easy Auth user lock skipped: allowed user object id is empty." >&2
    return
  fi

  local sp_id
  sp_id="$(az ad sp show --id "${ADMIN_EASYAUTH_CLIENT_ID}" --query id -o tsv 2>/dev/null || true)"
  if [ -z "${sp_id}" ]; then
    sp_id="$(az ad sp create --id "${ADMIN_EASYAUTH_CLIENT_ID}" --query id -o tsv 2>/dev/null || true)"
  fi
  if [ -z "${sp_id}" ]; then
    echo "Easy Auth user lock skipped: service principal not found for client id ${ADMIN_EASYAUTH_CLIENT_ID}" >&2
    return
  fi

  # Require explicit assignment so only assigned users can sign in.
  az rest \
    --method PATCH \
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/${sp_id}" \
    --headers "Content-Type=application/json" \
    --body '{"appRoleAssignmentRequired":true}' >/dev/null

  # Assign default app role to the allowed user. Ignore if already assigned.
  az rest \
    --method POST \
    --url "https://graph.microsoft.com/v1.0/users/${allowed_user_id}/appRoleAssignments" \
    --headers "Content-Type=application/json" \
    --body "{\"principalId\":\"${allowed_user_id}\",\"resourceId\":\"${sp_id}\",\"appRoleId\":\"00000000-0000-0000-0000-000000000000\"}" \
    >/dev/null 2>&1 || true
}

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
    enableAdminEasyAuth="$ADMIN_EASYAUTH_ENABLED" \
    adminEasyAuthTenantId="$ADMIN_EASYAUTH_TENANT_ID" \
    adminEasyAuthClientId="$ADMIN_EASYAUTH_CLIENT_ID" \
    adminEasyAuthClientSecret="$ADMIN_EASYAUTH_CLIENT_SECRET" \
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

enforce_admin_easyauth_user_lock

echo "Infrastructure deployed."
echo "Outputs saved to: $OUTPUTS_FILE"
jq '.' "$OUTPUTS_FILE"
