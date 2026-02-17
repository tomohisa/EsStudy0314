#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
ensure_paths
resolve_outputs

TAG="${2:-$(current_tag)}"
IMAGE_NAME="escqrsquestions-adminweb"

build_and_push_image "$ADMINWEB_PATH" "$IMAGE_NAME" "$TAG"

FRONTEND_FQDN="$(az containerapp show --resource-group "$RESOURCE_GROUP" --name "$FRONTEND_APP_NAME" --query properties.configuration.ingress.fqdn -o tsv)"
if [ -n "$FRONTEND_FQDN" ]; then
  CLIENT_BASE_URL="https://${FRONTEND_FQDN}"
else
  CLIENT_BASE_URL="http://${FRONTEND_APP_NAME}"
fi

az containerapp update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ADMINWEB_APP_NAME" \
  --image "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}" \
  --set-env-vars ASPNETCORE_URLS=http://+:8080 services__apiservice__http__0="http://${BACKEND_APP_NAME}" services__apiservice__https__0="http://${BACKEND_APP_NAME}" services__webfrontend__http__0="$CLIENT_BASE_URL" ClientBaseUrl="$CLIENT_BASE_URL"

echo "AdminWeb deployed: ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}"
echo "Admin ClientBaseUrl: $CLIENT_BASE_URL"
