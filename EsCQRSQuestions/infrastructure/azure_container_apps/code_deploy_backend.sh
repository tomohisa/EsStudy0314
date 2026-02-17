#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
ensure_paths
resolve_outputs

TAG="${2:-$(current_tag)}"
IMAGE_NAME="escqrsquestions-backend"

build_and_push_image "$BACKEND_PATH" "$IMAGE_NAME" "$TAG"

az containerapp update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BACKEND_APP_NAME" \
  --image "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}" \
  --set-env-vars ASPNETCORE_URLS=http://+:8080

echo "Backend deployed: ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${TAG}"
