#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Error: required command '$command_name' is not installed." >&2
    exit 1
  fi
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
  if [ -n "$LOGIN_COMMAND" ]; then
    echo "Running login command from config..."
    eval "$LOGIN_COMMAND"
  fi
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

  local dockerfile
  dockerfile="${app_dir}/Dockerfile.azurecontainerapps.tmp"

  cat > "$dockerfile" <<DOCKERFILE
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "$(basename "$csproj")" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "${dll_name}"]
DOCKERFILE

  echo "Building image ${ACR_LOGIN_SERVER}/${image_name}:${image_tag} from $app_dir"
  az acr build \
    --registry "$ACR_NAME" \
    --image "${image_name}:${image_tag}" \
    --file "$dockerfile" \
    "$app_dir"

  rm -f "$dockerfile"
}

current_tag() {
  date +%Y%m%d%H%M%S
}
