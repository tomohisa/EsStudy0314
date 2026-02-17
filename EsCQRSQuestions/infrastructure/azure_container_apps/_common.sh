#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AZ_RETRY_ATTEMPTS="${AZ_RETRY_ATTEMPTS:-3}"
AZ_RETRY_DELAY_SECONDS="${AZ_RETRY_DELAY_SECONDS:-5}"

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Error: required command '$command_name' is not installed." >&2
    exit 1
  fi
}

az_retry() {
  local attempt=1
  local exit_code=0
  while true; do
    if az "$@"; then
      return 0
    fi

    exit_code=$?
    if [ "$attempt" -ge "$AZ_RETRY_ATTEMPTS" ]; then
      echo "Azure CLI command failed after ${attempt} attempts: az $*" >&2
      return "$exit_code"
    fi

    echo "Azure CLI command failed (attempt ${attempt}/${AZ_RETRY_ATTEMPTS}), retrying in ${AZ_RETRY_DELAY_SECONDS}s: az $*" >&2
    sleep "$AZ_RETRY_DELAY_SECONDS"
    attempt=$((attempt + 1))
  done
}

load_config() {
  if [ -z "${1:-}" ]; then
    echo "Usage: <script> <environment-name>" >&2
    exit 1
  fi

  ENVIRONMENT="$1"
  CONFIG_FILE="${SCRIPT_DIR}/${ENVIRONMENT}.local.json"

  if [ ! -f "$CONFIG_FILE" ]; then
    echo "Error: configuration file not found: $CONFIG_FILE" >&2
    exit 1
  fi

  require_command jq
  require_command az

  RESOURCE_GROUP="$(jq -r '.resourceGroupName' "$CONFIG_FILE")"
  LOCATION="$(jq -r '.location' "$CONFIG_FILE")"
  BACKEND_PATH_RAW="$(jq -r '.backendRelativePath' "$CONFIG_FILE")"
  FRONTEND_PATH_RAW="$(jq -r '.frontendRelativePath' "$CONFIG_FILE")"
  ADMINWEB_PATH_RAW="$(jq -r '.adminwebRelativePath' "$CONFIG_FILE")"
  SUBSCRIPTION_ID="$(jq -r '.subscriptionId // empty' "$CONFIG_FILE")"
  LOG_RETENTION_IN_DAYS="$(jq -r '.logRetentionInDays // 30' "$CONFIG_FILE")"
  LOG_DAILY_QUOTA_GB="$(jq -r '.logDailyQuotaGb // "0.1"' "$CONFIG_FILE")"
  BACKEND_MIN_REPLICAS="$(jq -r '.backendMinReplicas // 1' "$CONFIG_FILE")"
  BACKEND_MAX_REPLICAS="$(jq -r '.backendMaxReplicas // 3' "$CONFIG_FILE")"
  FRONTEND_MIN_REPLICAS="$(jq -r '.frontendMinReplicas // 0' "$CONFIG_FILE")"
  FRONTEND_MAX_REPLICAS="$(jq -r '.frontendMaxReplicas // 2' "$CONFIG_FILE")"
  ADMINWEB_MIN_REPLICAS="$(jq -r '.adminwebMinReplicas // 0' "$CONFIG_FILE")"
  ADMINWEB_MAX_REPLICAS="$(jq -r '.adminwebMaxReplicas // 2' "$CONFIG_FILE")"
  LOGIN_COMMAND="$(jq -r '.logincommand // empty' "$CONFIG_FILE")"

  if [ "$RESOURCE_GROUP" = "null" ] || [ -z "$RESOURCE_GROUP" ]; then
    echo "Error: resourceGroupName is missing in $CONFIG_FILE" >&2
    exit 1
  fi

  if [ "$LOCATION" = "null" ] || [ -z "$LOCATION" ]; then
    echo "Error: location is missing in $CONFIG_FILE" >&2
    exit 1
  fi

  BACKEND_PATH="$(cd "$SCRIPT_DIR" && cd "$BACKEND_PATH_RAW" && pwd)"
  FRONTEND_PATH="$(cd "$SCRIPT_DIR" && cd "$FRONTEND_PATH_RAW" && pwd)"
  ADMINWEB_PATH="$(cd "$SCRIPT_DIR" && cd "$ADMINWEB_PATH_RAW" && pwd)"

  OUTPUTS_FILE="${SCRIPT_DIR}/${ENVIRONMENT}.outputs.json"
}

ensure_paths() {
  for p in "$BACKEND_PATH" "$FRONTEND_PATH" "$ADMINWEB_PATH"; do
    if [ ! -d "$p" ]; then
      echo "Error: directory does not exist: $p" >&2
      exit 1
    fi
  done
}

resolve_outputs() {
  if [ ! -f "$OUTPUTS_FILE" ]; then
    echo "Error: outputs file not found: $OUTPUTS_FILE" >&2
    echo "Run ./deploy_infra.sh $ENVIRONMENT first." >&2
    exit 1
  fi

  ACR_NAME="$(jq -r '.acrName' "$OUTPUTS_FILE")"
  ACR_LOGIN_SERVER="$(jq -r '.acrLoginServer' "$OUTPUTS_FILE")"
  BACKEND_APP_NAME="$(jq -r '.backendAppName' "$OUTPUTS_FILE")"
  FRONTEND_APP_NAME="$(jq -r '.frontendAppName' "$OUTPUTS_FILE")"
  ADMINWEB_APP_NAME="$(jq -r '.adminwebAppName' "$OUTPUTS_FILE")"

  for value_name in ACR_NAME ACR_LOGIN_SERVER BACKEND_APP_NAME FRONTEND_APP_NAME ADMINWEB_APP_NAME; do
    local value="${!value_name}"
    if [ "$value" = "null" ] || [ -z "$value" ]; then
      echo "Error: '$value_name' is missing in $OUTPUTS_FILE" >&2
      exit 1
    fi
  done
}

login_if_needed() {
  if az account show >/dev/null 2>&1; then
    return
  fi

  if [ -n "$LOGIN_COMMAND" ]; then
    echo "Running login command from config..."
    eval "$LOGIN_COMMAND"
  else
    echo "Error: Azure CLI is not logged in and logincommand is not configured." >&2
    exit 1
  fi
}

set_subscription_if_configured() {
  if [ -n "$SUBSCRIPTION_ID" ]; then
    az_retry account set --subscription "$SUBSCRIPTION_ID" >/dev/null
  fi
}

get_containerapp_fqdn() {
  local app_name="$1"
  local fqdn
  fqdn="$(az_retry containerapp show --resource-group "$RESOURCE_GROUP" --name "$app_name" --query properties.configuration.ingress.fqdn -o tsv | tr -d '\r')"
  if [ "$fqdn" = "null" ]; then
    fqdn=""
  fi
  echo "$fqdn"
}

wait_for_containerapp_ready() {
  local app_name="$1"
  local timeout_seconds="${2:-600}"
  local elapsed=0
  local interval=10
  local show_json
  local provisioning_state
  local running_state
  local latest_revision
  local latest_ready_revision
  local revision_health
  local revision_running

  echo "Waiting for container app '${app_name}' to become ready (timeout: ${timeout_seconds}s)..."
  while [ "$elapsed" -lt "$timeout_seconds" ]; do
    show_json="$(az_retry containerapp show \
      --resource-group "$RESOURCE_GROUP" \
      --name "$app_name" \
      -o json)"

    provisioning_state="$(printf '%s' "$show_json" | jq -r '.properties.provisioningState // empty')"
    running_state="$(printf '%s' "$show_json" | jq -r '.properties.runningStatus // empty')"
    latest_revision="$(printf '%s' "$show_json" | jq -r '.properties.latestRevisionName // empty')"
    latest_ready_revision="$(printf '%s' "$show_json" | jq -r '.properties.latestReadyRevisionName // empty')"

    if [ -n "$latest_revision" ]; then
      revision_health="$(az_retry containerapp revision show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$app_name" \
        --revision "$latest_revision" \
        --query "properties.healthState" \
        -o tsv | tr -d '\r')"
      revision_running="$(az_retry containerapp revision show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$app_name" \
        --revision "$latest_revision" \
        --query "properties.runningState" \
        -o tsv | tr -d '\r')"

      if [ "$revision_health" = "Unhealthy" ] || [ "$revision_running" = "Failed" ]; then
        echo "Latest revision '${latest_revision}' is not healthy (health='${revision_health}', running='${revision_running}')." >&2
        return 1
      fi
    fi

    if [ "$provisioning_state" = "Succeeded" ] &&
       { [ -z "$running_state" ] || [ "$running_state" = "Running" ]; } &&
       [ -n "$latest_revision" ] &&
       [ "$latest_revision" = "$latest_ready_revision" ]; then
      echo "Container app '${app_name}' is ready on revision '${latest_revision}'."
      return 0
    fi

    echo "Current state: provisioning='${provisioning_state:-unknown}', running='${running_state:-unknown}', latest='${latest_revision:-none}', ready='${latest_ready_revision:-none}'"
    sleep "$interval"
    elapsed=$((elapsed + interval))
  done

  echo "Timed out waiting for container app '${app_name}' to become ready." >&2
  return 1
}

find_project_file() {
  local project_dir="$1"
  local csproj
  csproj="$(find "$project_dir" -maxdepth 1 -name '*.csproj' | head -n 1)"
  if [ -z "$csproj" ]; then
    echo "Error: no .csproj found in $project_dir" >&2
    exit 1
  fi
  echo "$csproj"
}

build_and_push_image() {
  local app_dir="$1"
  local image_name="$2"
  local image_tag="$3"

  local csproj
  csproj="$(find_project_file "$app_dir")"
  local dll_name
  dll_name="$(basename "$csproj" .csproj).dll"
  local project_dir_name
  project_dir_name="$(basename "$app_dir")"
  local project_file_name
  project_file_name="$(basename "$csproj")"
  local solution_root
  solution_root="$(cd "$app_dir/.." && pwd)"

  local dockerfile
  dockerfile="${solution_root}/Dockerfile.azurecontainerapps.${project_dir_name}.tmp"

  cat > "$dockerfile" <<DOCKERFILE
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "${project_dir_name}/${project_file_name}" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "${dll_name}"]
DOCKERFILE

  echo "Building image ${ACR_LOGIN_SERVER}/${image_name}:${image_tag} from $app_dir"
  az_retry acr build \
    --registry "$ACR_NAME" \
    --image "${image_name}:${image_tag}" \
    --file "$dockerfile" \
    "$solution_root"

  rm -f "$dockerfile"
}

current_tag() {
  date +%Y%m%d%H%M%S
}
