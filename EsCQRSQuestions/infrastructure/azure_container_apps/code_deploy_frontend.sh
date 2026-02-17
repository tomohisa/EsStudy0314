#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
login_if_needed
set_subscription_if_configured
ensure_paths
resolve_outputs

TAG="${2:-$(current_tag)}"
IMAGE_NAME="escqrsquestions-frontend"

build_and_push_image "$FRONTEND_PATH" "$IMAGE_NAME" "$TAG"

wait_for_containerapp_ready "$BACKEND_APP_NAME"
BACKEND_FQDN="$(get_containerapp_fqdn "$BACKEND_APP_NAME")"
if [ -n "$BACKEND_FQDN" ]; then
  API_BASE_URL="https://${BACKEND_FQDN}"
else
  API_BASE_URL="http://${BACKEND_APP_NAME}"
fi

az_retry containerapp update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FRONTEND_APP_NAME" \
  --image "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}" \
  --set-env-vars ASPNETCORE_URLS=http://+:8080 services__apiservice__http__0="$API_BASE_URL" services__apiservice__https__0="$API_BASE_URL"

wait_for_containerapp_ready "$FRONTEND_APP_NAME"

echo "Frontend deployed: ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}"
