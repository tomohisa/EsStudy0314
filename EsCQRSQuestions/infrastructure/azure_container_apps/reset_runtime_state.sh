#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

load_config "${1:-}"
login_if_needed
set_subscription_if_configured
resolve_outputs

COSMOS_ACCOUNT_NAME="$(jq -r '.cosmosAccountName // empty' "$OUTPUTS_FILE")"
STORAGE_ACCOUNT_NAME="$(jq -r '.storageAccountName // empty' "$OUTPUTS_FILE")"
BACKEND_APP_NAME="$(jq -r '.backendAppName // empty' "$OUTPUTS_FILE")"
FRONTEND_APP_NAME="$(jq -r '.frontendAppName // empty' "$OUTPUTS_FILE")"
ADMINWEB_APP_NAME="$(jq -r '.adminwebAppName // empty' "$OUTPUTS_FILE")"

if [ -z "$COSMOS_ACCOUNT_NAME" ] || [ -z "$STORAGE_ACCOUNT_NAME" ]; then
  echo "Required outputs are missing in $OUTPUTS_FILE" >&2
  exit 1
fi

echo "Resetting runtime state for environment: $ENVIRONMENT"
echo "  resourceGroup: $RESOURCE_GROUP"
echo "  cosmosAccount: $COSMOS_ACCOUNT_NAME"
echo "  storageAccount: $STORAGE_ACCOUNT_NAME"

delete_cosmos_db_if_exists() {
  local db_name="$1"
  if az_retry cosmosdb sql database show \
      --account-name "$COSMOS_ACCOUNT_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --name "$db_name" \
      >/dev/null 2>&1; then
    echo "Deleting Cosmos SQL database: $db_name"
    az_retry cosmosdb sql database delete \
      --account-name "$COSMOS_ACCOUNT_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --name "$db_name" \
      --yes >/dev/null
  else
    echo "Cosmos SQL database not found (skip): $db_name"
  fi
}

# Remove old/new Sekiban runtime stores and Orleans state store.
delete_cosmos_db_if_exists "SekibanDcb"
delete_cosmos_db_if_exists "SekibanDb"
delete_cosmos_db_if_exists "Orleans"

echo "Deleting all Azure Storage queues..."
while IFS= read -r queue_name; do
  if [ -n "$queue_name" ]; then
    echo "  delete queue: $queue_name"
    az_retry storage queue delete \
      --account-name "$STORAGE_ACCOUNT_NAME" \
      --name "$queue_name" \
      --auth-mode login >/dev/null || true
  fi
done < <(az_retry storage queue list \
  --account-name "$STORAGE_ACCOUNT_NAME" \
  --auth-mode login \
  --query '[].name' -o tsv | tr -d '\r')

echo "Deleting all Azure Storage tables..."
while IFS= read -r table_name; do
  if [ -n "$table_name" ]; then
    echo "  delete table: $table_name"
    az_retry storage table delete \
      --account-name "$STORAGE_ACCOUNT_NAME" \
      --name "$table_name" \
      --auth-mode login \
      --yes >/dev/null || true
  fi
done < <(az_retry storage table list \
  --account-name "$STORAGE_ACCOUNT_NAME" \
  --auth-mode login \
  --query '[].name' -o tsv | tr -d '\r')

echo "Re-applying infrastructure (recreate Orleans DB/containers, app settings)..."
"${SCRIPT_DIR}/deploy_infra.sh" "$ENVIRONMENT"

echo "Restarting container apps..."
for app in "$BACKEND_APP_NAME" "$FRONTEND_APP_NAME" "$ADMINWEB_APP_NAME"; do
  if [ -n "$app" ] && [ "$app" != "null" ]; then
    echo "  restart: $app"
    az_retry containerapp revision restart \
      --resource-group "$RESOURCE_GROUP" \
      --name "$app" \
      --revision "$(az_retry containerapp show --resource-group "$RESOURCE_GROUP" --name "$app" --query properties.latestRevisionName -o tsv | tr -d '\r')" >/dev/null
  fi
done

echo "Runtime state reset completed."
