#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
ensure_paths
resolve_outputs

TAG="${2:-$(current_tag)}"
IMAGE_NAME="escqrsquestions-frontend"

build_and_push_image "$FRONTEND_PATH" "$IMAGE_NAME" "$TAG"

az containerapp update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FRONTEND_APP_NAME" \
  --image "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}" \
  --set-env-vars ASPNETCORE_URLS=http://+:8080 services__apiservice__http__0="http://${BACKEND_APP_NAME}" services__apiservice__https__0="http://${BACKEND_APP_NAME}"

echo "Frontend deployed: ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}"
