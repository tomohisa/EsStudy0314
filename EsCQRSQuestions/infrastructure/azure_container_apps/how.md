# Azure Container Apps Deployment Guide

This folder provides `az + bicep + sh` deployment scripts for Azure Container Apps.
It mirrors the `azure_appservice` style and uses `*.local.json` environment files.

## 1. Prepare local config

Use one of:
- `dnl.local.json`
- `dpt.local.json`
- `ecq.local.json`

or create your own `<env>.local.json`.

Required keys:

```json
{
  "resourceGroupName": "your-rg-name",
  "location": "japaneast",
  "backendRelativePath": "../../EsCQRSQuestions.ApiService",
  "frontendRelativePath": "../../EsCQRSQuestions.Web",
  "adminwebRelativePath": "../../EsCQRSQuestions.AdminWeb",
  "logRetentionInDays": 30,
  "logDailyQuotaGb": "0.1",
  "backendMinReplicas": 1,
  "backendMaxReplicas": 3,
  "frontendMinReplicas": 0,
  "frontendMaxReplicas": 2,
  "adminwebMinReplicas": 0,
  "adminwebMaxReplicas": 2,
  "adminEasyAuth": {
    "enabled": false,
    "tenantId": "",
    "clientId": "",
    "clientSecret": "",
    "appDisplayName": "EsCQRSQuestions-AdminWeb-your-rg-name",
    "allowedUserObjectId": ""
  },
  "logincommand": "az login --tenant <tenant> --use-device-code"
}
```

### AdminWeb Easy Auth (Entra ID)

If you want to lock AdminWeb to Entra ID login and a specific user:

```bash
./configure_admin_easyauth.sh dnl
./deploy_infra.sh dnl
```

- `configure_admin_easyauth.sh` creates/updates app registration, callback URI, and local config.
- `deploy_infra.sh` applies Easy Auth declaratively via Bicep (`authConfigs`) and enforces `appRoleAssignmentRequired=true`.
- `allowedUserObjectId` (or signed-in user fallback) is assigned so only that user can access AdminWeb.

## 2. Create resource group

```bash
chmod +x ./*.sh
./create_resource_group.sh dnl
```

## 3. Deploy infrastructure (Bicep)

```bash
./deploy_infra.sh dnl
```

Outputs are saved to `dnl.outputs.json`.

## 4. Deploy code to Container Apps

```bash
./code_deploy_backend.sh dnl
./code_deploy_frontend.sh dnl
./code_deploy_adminweb.sh dnl
```

## 5. One-shot deploy (infra + all apps)

```bash
./deploy_all.sh dnl
```

Optional second argument is image tag:

```bash
./deploy_all.sh dnl 202602170001
```

`deploy_all.sh` now includes an idempotent projection-consistency repair step:
- Detects mismatch between `SekibanDcb.events` and derived projection states
- Safely deactivates backend revision before deleting derived states
- Reactivates backend and waits for rebuild

Manual repair command:

```bash
./repair_projection_state.sh dnl
```

Force repair (even if no mismatch is detected):

```bash
./repair_projection_state.sh dnl --force
```

## 6. Full runtime reset (safe ordering)

```bash
./reset_runtime_state.sh dnl
```

This script now deactivates app revisions before deleting Cosmos/Storage runtime state to prevent stale in-memory projection snapshots from being written back during shutdown.

## Notes

- Scripts use `az acr build`, so local Docker daemon is not required.
- Each deploy script builds a temporary Dockerfile in each project folder and removes it afterward.
- AdminWeb `ClientBaseUrl` is automatically set from frontend ingress FQDN when available.
- `deploy_infra.sh` automatically configures Container Apps Environment telemetry to Application Insights.
- Cost stabilization knobs are configurable per environment via `*.local.json`:
  - Log retention (`logRetentionInDays`)
  - Log daily cap (`logDailyQuotaGb`)
  - Per-app min/max replicas
