#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
login_if_needed
set_subscription_if_configured

if [ ! -f "$OUTPUTS_FILE" ]; then
  echo "Outputs file not found: $OUTPUTS_FILE" >&2
  echo "Run deploy_infra first to generate admin FQDN." >&2
  exit 1
fi

ADMIN_FQDN="$(jq -r '.adminwebFqdn // empty' "$OUTPUTS_FILE")"
if [ -z "$ADMIN_FQDN" ]; then
  echo "adminwebFqdn is missing in outputs: $OUTPUTS_FILE" >&2
  exit 1
fi

CONFIG_FILE="${SCRIPT_DIR}/${ENVIRONMENT}.local.json"
REDIRECT_URI="https://${ADMIN_FQDN}/.auth/login/aad/callback"
TENANT_ID="$(jq -r '.adminEasyAuth.tenantId // empty' "$CONFIG_FILE")"
if [ -z "$TENANT_ID" ]; then
  TENANT_ID="$(az account show --query tenantId -o tsv)"
fi

CLIENT_ID="$(jq -r '.adminEasyAuth.clientId // empty' "$CONFIG_FILE")"
CLIENT_SECRET="$(jq -r '.adminEasyAuth.clientSecret // empty' "$CONFIG_FILE")"
DISPLAY_NAME="$(jq -r '.adminEasyAuth.appDisplayName // empty' "$CONFIG_FILE")"
if [ -z "$DISPLAY_NAME" ]; then
  DISPLAY_NAME="EsCQRSQuestions-AdminWeb-${RESOURCE_GROUP}"
fi

if [ -z "$CLIENT_ID" ]; then
  CLIENT_ID="$(az ad app list --display-name "$DISPLAY_NAME" --query '[0].appId' -o tsv 2>/dev/null || true)"
fi

if [ -z "$CLIENT_ID" ]; then
  CLIENT_ID="$(az ad app create --display-name "$DISPLAY_NAME" --sign-in-audience AzureADMyOrg --web-redirect-uris "$REDIRECT_URI" --query appId -o tsv)"
else
  CURRENT_URIS="$(az ad app show --id "$CLIENT_ID" --query 'web.redirectUris' -o tsv | tr '\t' '\n' || true)"
  if ! printf '%s\n' "$CURRENT_URIS" | rg -qx "$REDIRECT_URI"; then
    UPDATED_URIS="$(printf '%s\n%s\n' "$CURRENT_URIS" "$REDIRECT_URI" | rg -v '^$' | awk '!seen[$0]++')"
    if [ -n "$UPDATED_URIS" ]; then
      # shellcheck disable=SC2086
      az ad app update --id "$CLIENT_ID" --web-redirect-uris $UPDATED_URIS >/dev/null
    fi
  fi
fi

if [ -z "$CLIENT_SECRET" ]; then
  CLIENT_SECRET="$(az ad app credential reset --id "$CLIENT_ID" --append --display-name "aca-admin-easyauth" --years 2 --query password -o tsv)"
fi

ALLOWED_USER_ID="$(jq -r '.adminEasyAuth.allowedUserObjectId // empty' "$CONFIG_FILE")"
if [ -z "$ALLOWED_USER_ID" ]; then
  ALLOWED_USER_ID="$(az ad signed-in-user show --query id -o tsv)"
fi

TMP_FILE="${CONFIG_FILE}.tmp"
jq \
  --arg tenantId "$TENANT_ID" \
  --arg clientId "$CLIENT_ID" \
  --arg clientSecret "$CLIENT_SECRET" \
  --arg appDisplayName "$DISPLAY_NAME" \
  --arg allowedUserObjectId "$ALLOWED_USER_ID" \
  '.adminEasyAuth = ((.adminEasyAuth // {}) + {enabled:true, tenantId:$tenantId, clientId:$clientId, clientSecret:$clientSecret, appDisplayName:$appDisplayName, allowedUserObjectId:$allowedUserObjectId})' \
  "$CONFIG_FILE" > "$TMP_FILE"
mv "$TMP_FILE" "$CONFIG_FILE"

echo "Admin Easy Auth config updated in: $CONFIG_FILE"
echo "  tenantId: $TENANT_ID"
echo "  clientId: $CLIENT_ID"
echo "  allowedUserObjectId: $ALLOWED_USER_ID"
echo "  redirectUri: $REDIRECT_URI"
