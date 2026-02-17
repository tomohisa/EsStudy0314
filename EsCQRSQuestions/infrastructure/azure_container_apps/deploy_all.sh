#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
TAG="${2:-$(current_tag)}"

login_if_needed
set_subscription_if_configured
"${SCRIPT_DIR}/create_resource_group.sh" "$ENVIRONMENT"
"${SCRIPT_DIR}/deploy_infra.sh" "$ENVIRONMENT"
"${SCRIPT_DIR}/code_deploy_backend.sh" "$ENVIRONMENT" "$TAG"
"${SCRIPT_DIR}/code_deploy_frontend.sh" "$ENVIRONMENT" "$TAG"
"${SCRIPT_DIR}/code_deploy_adminweb.sh" "$ENVIRONMENT" "$TAG"

echo "All deployments completed for environment: ${ENVIRONMENT}"
echo "Image tag: ${TAG}"
