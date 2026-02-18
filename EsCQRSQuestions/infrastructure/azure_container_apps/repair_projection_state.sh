#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "${SCRIPT_DIR}/_common.sh"

usage() {
  cat <<'EOF'
Usage:
  ./repair_projection_state.sh <environment-name> [--force]

Description:
  Detect and repair projection state drift where:
  - SekibanDcb.events has fewer records than projection states (multiProjectionStates),
  - or projection state in OrleansStorage (multiProjection/tagStateCache) is stale.

Safe repair sequence:
  1) Deactivate backend revision (stop stale writeback)
  2) Delete derived projection states
  3) Reactivate backend revision
  4) Wait for projection states to be rebuilt

Options:
  --force   Run repair even when no mismatch is detected.
EOF
}

if [ $# -lt 1 ]; then
  usage
  exit 1
fi

ENV_ARG="$1"
FORCE_REPAIR="false"
if [ "${2:-}" = "--force" ]; then
  FORCE_REPAIR="true"
elif [ -n "${2:-}" ]; then
  echo "Unknown option: ${2}" >&2
  usage
  exit 1
fi

load_config "$ENV_ARG"
login_if_needed
set_subscription_if_configured
resolve_outputs

COSMOS_ACCOUNT_NAME="$(jq -r '.cosmosAccountName // empty' "$OUTPUTS_FILE")"
BACKEND_APP_NAME="$(jq -r '.backendAppName // empty' "$OUTPUTS_FILE")"

if [ -z "$COSMOS_ACCOUNT_NAME" ] || [ "$COSMOS_ACCOUNT_NAME" = "null" ]; then
  echo "Required output missing: cosmosAccountName in $OUTPUTS_FILE" >&2
  exit 1
fi
if [ -z "$BACKEND_APP_NAME" ] || [ "$BACKEND_APP_NAME" = "null" ]; then
  echo "Required output missing: backendAppName in $OUTPUTS_FILE" >&2
  exit 1
fi

deactivate_backend() {
  local rev
  rev="$(az_retry containerapp show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$BACKEND_APP_NAME" \
    --query properties.latestRevisionName -o tsv | tr -d '\r')"
  if [ -n "$rev" ] && [ "$rev" != "null" ]; then
    echo "Deactivating backend revision: $rev"
    az_retry containerapp revision deactivate \
      --resource-group "$RESOURCE_GROUP" \
      --name "$BACKEND_APP_NAME" \
      --revision "$rev" >/dev/null || true
    echo "$rev"
  fi
}

activate_backend() {
  local rev="$1"
  if [ -n "$rev" ] && [ "$rev" != "null" ]; then
    echo "Activating backend revision: $rev"
    az_retry containerapp revision activate \
      --resource-group "$RESOURCE_GROUP" \
      --name "$BACKEND_APP_NAME" \
      --revision "$rev" >/dev/null
  fi
}

echo "Checking projection consistency for environment: $ENVIRONMENT"
echo "  resourceGroup: $RESOURCE_GROUP"
echo "  backendApp: $BACKEND_APP_NAME"
echo "  cosmosAccount: $COSMOS_ACCOUNT_NAME"

NEEDS_REPAIR="$(python3 - "$RESOURCE_GROUP" "$COSMOS_ACCOUNT_NAME" <<'PY'
import json
import subprocess
import sys
from azure.cosmos import CosmosClient

resource_group = sys.argv[1]
account_name = sys.argv[2]

key = subprocess.check_output([
    "az","cosmosdb","keys","list",
    "--name",account_name,
    "--resource-group",resource_group,
    "--query","primaryMasterKey",
    "-o","tsv"
], text=True).strip()
endpoint = subprocess.check_output([
    "az","cosmosdb","show",
    "--name",account_name,
    "--resource-group",resource_group,
    "--query","documentEndpoint",
    "-o","tsv"
], text=True).strip()

client = CosmosClient(endpoint, key)

events = client.get_database_client("SekibanDcb").get_container_client("events")
states = client.get_database_client("SekibanDcb").get_container_client("multiProjectionStates")

events_count = list(events.query_items(
    "SELECT VALUE COUNT(1) FROM c",
    enable_cross_partition_query=True
))[0]

rows = list(states.query_items(
    "SELECT c.projectorName, c.eventsProcessed FROM c",
    enable_cross_partition_query=True
))
max_processed = max((int(r.get("eventsProcessed", 0)) for r in rows), default=0)

# Repair rule:
# - stale derived state (processed events exceeds actual event store count)
# - or no states exist while events exist (force rebuild)
needs_repair = max_processed > events_count or (events_count > 0 and len(rows) == 0)

print(json.dumps({
    "eventsCount": events_count,
    "stateCount": len(rows),
    "maxEventsProcessed": max_processed,
    "needsRepair": needs_repair
}))
PY
)"

echo "Check result: $NEEDS_REPAIR"

REPAIR_FLAG="$(echo "$NEEDS_REPAIR" | jq -r '.needsRepair')"
if [ "$FORCE_REPAIR" != "true" ] && [ "$REPAIR_FLAG" != "true" ]; then
  echo "Projection state is consistent. No repair needed."
  exit 0
fi

echo "Repair is required (or forced). Starting safe repair sequence..."
BACKEND_REVISION="$(deactivate_backend || true)"
sleep 5

python3 - "$RESOURCE_GROUP" "$COSMOS_ACCOUNT_NAME" <<'PY'
import subprocess
import sys
from azure.cosmos import CosmosClient

resource_group = sys.argv[1]
account_name = sys.argv[2]

key = subprocess.check_output([
    "az","cosmosdb","keys","list",
    "--name",account_name,
    "--resource-group",resource_group,
    "--query","primaryMasterKey",
    "-o","tsv"
], text=True).strip()
endpoint = subprocess.check_output([
    "az","cosmosdb","show",
    "--name",account_name,
    "--resource-group",resource_group,
    "--query","documentEndpoint",
    "-o","tsv"
], text=True).strip()

client = CosmosClient(endpoint, key)

sekiban_mp = client.get_database_client("SekibanDcb").get_container_client("multiProjectionStates")
orleans_storage = client.get_database_client("Orleans").get_container_client("OrleansStorage")

mp_items = list(sekiban_mp.query_items(
    "SELECT c.id, c.pk FROM c",
    enable_cross_partition_query=True
))
for item in mp_items:
    sekiban_mp.delete_item(item=item["id"], partition_key=item["pk"])

orleans_items = list(orleans_storage.query_items(
    "SELECT c.id, c.PartitionKey FROM c WHERE c.PartitionKey IN ('multiProjection', 'tagStateCache')",
    enable_cross_partition_query=True
))
for item in orleans_items:
    orleans_storage.delete_item(item=item["id"], partition_key=item["PartitionKey"])

print(f"Deleted SekibanDcb.multiProjectionStates: {len(mp_items)}")
print(f"Deleted Orleans.OrleansStorage (multiProjection/tagStateCache): {len(orleans_items)}")
PY

activate_backend "$BACKEND_REVISION"
wait_for_containerapp_ready "$BACKEND_APP_NAME" 600

echo "Waiting for projection states to rebuild..."
for i in $(seq 1 18); do
  SNAPSHOT="$(python3 - "$RESOURCE_GROUP" "$COSMOS_ACCOUNT_NAME" <<'PY'
import json
import subprocess
import sys
from azure.cosmos import CosmosClient

resource_group = sys.argv[1]
account_name = sys.argv[2]

key = subprocess.check_output([
    "az","cosmosdb","keys","list",
    "--name",account_name,
    "--resource-group",resource_group,
    "--query","primaryMasterKey",
    "-o","tsv"
], text=True).strip()
endpoint = subprocess.check_output([
    "az","cosmosdb","show",
    "--name",account_name,
    "--resource-group",resource_group,
    "--query","documentEndpoint",
    "-o","tsv"
], text=True).strip()

client = CosmosClient(endpoint, key)
events = client.get_database_client("SekibanDcb").get_container_client("events")
states = client.get_database_client("SekibanDcb").get_container_client("multiProjectionStates")

events_count = list(events.query_items(
    "SELECT VALUE COUNT(1) FROM c",
    enable_cross_partition_query=True
))[0]
rows = list(states.query_items(
    "SELECT c.projectorName, c.eventsProcessed FROM c",
    enable_cross_partition_query=True
))
max_processed = max((int(r.get("eventsProcessed", 0)) for r in rows), default=0)
print(json.dumps({
    "eventsCount": events_count,
    "stateCount": len(rows),
    "maxEventsProcessed": max_processed
}))
PY
)"

  echo "  rebuild-check[$i]: $SNAPSHOT"
  STATE_COUNT="$(echo "$SNAPSHOT" | jq -r '.stateCount')"
  EVENTS_COUNT="$(echo "$SNAPSHOT" | jq -r '.eventsCount')"
  MAX_PROCESSED="$(echo "$SNAPSHOT" | jq -r '.maxEventsProcessed')"

  if [ "$EVENTS_COUNT" -eq 0 ]; then
    # Nothing to rebuild, but repair still valid.
    break
  fi

  if [ "$STATE_COUNT" -gt 0 ] && [ "$MAX_PROCESSED" -le "$EVENTS_COUNT" ]; then
    break
  fi

  sleep 5
done

echo "Projection repair completed."
