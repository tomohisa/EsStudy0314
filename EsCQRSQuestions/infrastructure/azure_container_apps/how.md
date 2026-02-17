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
  "logincommand": "az login --tenant <tenant> --use-device-code"
}
```

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

## Notes

- Scripts use `az acr build`, so local Docker daemon is not required.
- Each deploy script builds a temporary Dockerfile in each project folder and removes it afterward.
- AdminWeb `ClientBaseUrl` is automatically set from frontend ingress FQDN when available.
- `deploy_infra.sh` automatically configures Container Apps Environment telemetry to Application Insights.
- Cost stabilization knobs are configurable per environment via `*.local.json`:
  - Log retention (`logRetentionInDays`)
  - Log daily cap (`logDailyQuotaGb`)
  - Per-app min/max replicas
