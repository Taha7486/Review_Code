# CodeReview — AKS Migration Plan
> **Goal:** Deploy the full 7-service CodeReview stack to Azure Kubernetes Service using Terraform (IaC), ACR (image registry), Azure Key Vault (secrets), Nginx Ingress (routing), and ArgoCD (GitOps). ArgoCD managed via `kubectl port-forward`.

---

## Overview

| Property | Value |
|---|---|
| Region | West Europe |
| Node VM Size | Standard_B2s (2 vCPU, 4GB RAM) |
| Node Count | 1 (scale to 2 if memory pressure) |
| Exposure | Azure default `.cloudapp.azure.com` via Nginx Ingress |
| ArgoCD Access | `kubectl port-forward` only |
| MySQL | Fresh instance (no data migration) |
| Image Registry | Azure Container Registry (ACR) replacing DockerHub |
| Secret Management | Azure Key Vault + Secrets Store CSI Driver |

---

## Phase 0 — Prerequisites (Do This Before Credits Arrive)

### 0.1 Install Required Tools

```bash
# Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az login

# Terraform
sudo apt-get install terraform
# or via tfenv for version management

# kubectl
az aks install-cli

# Helm (for ArgoCD and Nginx Ingress installation)
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
```

### 0.2 Create Terraform Directory Structure

Create this folder structure in the root of your project:

```
terraform/
├── main.tf          # Provider config and Resource Group
├── acr.tf           # Azure Container Registry
├── aks.tf           # AKS Cluster, node pool, CSI driver
├── keyvault.tf      # Key Vault + secrets
├── network.tf       # VNet and subnets (optional but clean)
├── variables.tf     # All input variables
└── outputs.tf       # Kubeconfig, ACR login server, Key Vault URI
```

### 0.3 Identify Your Current Secrets

Collect these values — you'll load them into Key Vault:

- `JWT_SECRET` — from dotnet-secrets
- `DB_PASSWORD` — from mysql-secret
- `GITHUB_PAT` — if used in CI or the app

---

## Phase 1 — Terraform: Provision Azure Infrastructure

### 1.1 `variables.tf`

```hcl
variable "location" {
  default = "West Europe"
}

variable "resource_group_name" {
  default = "rg-codereview"
}

variable "acr_name" {
  default = "acrcodereviewtaha" # must be globally unique, lowercase, no hyphens
}

variable "aks_cluster_name" {
  default = "aks-codereview"
}

variable "node_count" {
  default = 1
}

variable "node_vm_size" {
  default = "Standard_B2s"
}

variable "key_vault_name" {
  default = "kv-codereview-taha" # must be globally unique
}

# Secrets — pass via CLI or .tfvars (never commit to git)
variable "jwt_secret" {
  sensitive = true
}

variable "db_password" {
  sensitive = true
}

variable "github_pat" {
  sensitive = true
  default   = ""
}
```

### 1.2 `main.tf`

```hcl
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
}

data "azurerm_client_config" "current" {}
```

### 1.3 `acr.tf`

```hcl
resource "azurerm_container_registry" "acr" {
  name                = var.acr_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false # AKS pulls via Managed Identity, not admin creds
}

# Grant AKS kubelet identity the AcrPull role
resource "azurerm_role_assignment" "aks_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}
```

### 1.4 `aks.tf`

```hcl
resource "azurerm_kubernetes_cluster" "aks" {
  name                = var.aks_cluster_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "codereview"

  default_node_pool {
    name       = "default"
    node_count = var.node_count
    vm_size    = var.node_vm_size
  }

  identity {
    type = "SystemAssigned"
  }

  # Enable Secret Store CSI Driver for Key Vault integration
  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  network_profile {
    network_plugin = "azure"
    load_balancer_sku = "standard"
  }
}

# Grant AKS identity access to Key Vault
resource "azurerm_role_assignment" "aks_keyvault_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_kubernetes_cluster.aks.key_vault_secrets_provider[0].secret_identity[0].object_id
}
```

### 1.5 `keyvault.tf`

```hcl
resource "azurerm_key_vault" "main" {
  name                = var.key_vault_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku_name            = "standard"
  tenant_id           = data.azurerm_client_config.current.tenant_id

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    secret_permissions = ["Get", "List", "Set", "Delete", "Purge"]
  }
}

resource "azurerm_key_vault_secret" "jwt_secret" {
  name         = "jwt-secret"
  value        = var.jwt_secret
  key_vault_id = azurerm_key_vault.main.id
}

resource "azurerm_key_vault_secret" "db_password" {
  name         = "db-password"
  value        = var.db_password
  key_vault_id = azurerm_key_vault.main.id
}

resource "azurerm_key_vault_secret" "github_pat" {
  count        = var.github_pat != "" ? 1 : 0
  name         = "github-pat"
  value        = var.github_pat
  key_vault_id = azurerm_key_vault.main.id
}
```

### 1.6 `outputs.tf`

```hcl
output "acr_login_server" {
  value = azurerm_container_registry.acr.login_server
}

output "aks_cluster_name" {
  value = azurerm_kubernetes_cluster.aks.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.main.vault_uri
}

output "keyvault_tenant_id" {
  value = data.azurerm_client_config.current.tenant_id
}

output "keyvault_client_id" {
  value = azurerm_kubernetes_cluster.aks.key_vault_secrets_provider[0].secret_identity[0].client_id
}
```

### 1.7 Run Terraform

```bash
cd terraform/

terraform init

# Pass secrets via CLI flags (never put real values in .tfvars committed to git)
terraform plan \
  -var="jwt_secret=your_actual_jwt_secret" \
  -var="db_password=your_actual_db_password"

terraform apply \
  -var="jwt_secret=your_actual_jwt_secret" \
  -var="db_password=your_actual_db_password"
```

### 1.8 Connect kubectl to AKS

```bash
az aks get-credentials \
  --resource-group rg-codereview \
  --name aks-codereview

# Verify
kubectl get nodes
```

---

## Phase 2 — Migrate Images from DockerHub to ACR

### 2.1 Log into ACR

```bash
# Get ACR login server from Terraform output
ACR_SERVER=$(terraform output -raw acr_login_server)

az acr login --name acrcodereviewtaha
```

### 2.2 Retag and Push All Images

```bash
# For each service, pull from DockerHub, retag, push to ACR
# Replace <acr-server> with your actual ACR login server output

services=("code-review-api" "code-review-php" "code-review-frontend")

for svc in "${services[@]}"; do
  docker pull taha7486/$svc:latest
  docker tag taha7486/$svc:latest $ACR_SERVER/$svc:latest
  docker push $ACR_SERVER/$svc:latest
done
```

### 2.3 Update GitHub Actions (`ci.yml`)

Replace your DockerHub login/push steps with:

```yaml
- name: Azure Container Registry Login
  uses: azure/docker-login@v1
  with:
    login-server: ${{ secrets.ACR_LOGIN_SERVER }}
    username: ${{ secrets.ACR_USERNAME }}
    password: ${{ secrets.ACR_PASSWORD }}

- name: Build and Push to ACR
  run: |
    docker build -t ${{ secrets.ACR_LOGIN_SERVER }}/code-review-api:${{ github.sha }} ./api
    docker push ${{ secrets.ACR_LOGIN_SERVER }}/code-review-api:${{ github.sha }}
    # Repeat for php-service and react-app
```

Add these GitHub Actions secrets:
- `ACR_LOGIN_SERVER` — from Terraform output `acr_login_server`
- `ACR_USERNAME` — from `az acr credential show --name acrcodereviewtaha --query username`
- `ACR_PASSWORD` — from `az acr credential show --name acrcodereviewtaha --query passwords[0].value`

---

## Phase 3 — Update Kubernetes Manifests

### 3.1 Update Image References

In every deployment manifest, change image references from:
```yaml
image: taha7486/code-review-api:latest
```
To:
```yaml
image: acrcodereviewtaha.azurecr.io/code-review-api:latest
```

### 3.2 Change Service Types

Change `dotnet-api` and `react-app` services from `NodePort` to `ClusterIP`:

```yaml
# Before
spec:
  type: NodePort

# After
spec:
  type: ClusterIP
```

### 3.3 Fix MySQL PVC Storage Class

Update the MySQL PersistentVolumeClaim to use Azure Managed Disk:

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: mysql-pvc
spec:
  accessModes:
    - ReadWriteOnce
  storageClassName: managed-csi   # Azure managed disk
  resources:
    requests:
      storage: 5Gi
```

### 3.4 Create SecretProviderClass for Key Vault

Create a new file `k8s/secret-provider-class.yaml`:

```yaml
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: codereview-kv-secrets
spec:
  provider: azure
  parameters:
    usePodIdentity: "false"
    clientID: "<keyvault_client_id from terraform output>"
    keyvaultName: "kv-codereview-taha"
    cloudName: ""
    objects: |
      array:
        - |
          objectName: jwt-secret
          objectType: secret
        - |
          objectName: db-password
          objectType: secret
    tenantId: "<keyvault_tenant_id from terraform output>"
  secretObjects:
    - secretName: dotnet-secrets
      type: Opaque
      data:
        - objectName: jwt-secret
          key: JWT_SECRET
    - secretName: mysql-secret
      type: Opaque
      data:
        - objectName: db-password
          key: MYSQL_ROOT_PASSWORD
```

### 3.5 Update dotnet-api Deployment to Use CSI Secrets

Add a volume mount to your dotnet-api deployment:

```yaml
spec:
  template:
    spec:
      volumes:
        - name: secrets-store
          csi:
            driver: secrets-store.csi.k8s.io
            readOnly: true
            volumeAttributes:
              secretProviderClass: "codereview-kv-secrets"
      containers:
        - name: dotnet-api
          volumeMounts:
            - name: secrets-store
              mountPath: "/mnt/secrets"
              readOnly: true
          envFrom:
            - secretRef:
                name: dotnet-secrets   # populated by CSI driver
```

### 3.6 Create Nginx Ingress Resource

Create `k8s/ingress.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: codereview-ingress
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  ingressClassName: nginx
  rules:
    - http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: react-app
                port:
                  number: 80
          - path: /api
            pathType: Prefix
            backend:
              service:
                name: dotnet-api
                port:
                  number: 80
```

---

## Phase 4 — Install Cluster Components

### 4.1 Install Nginx Ingress Controller

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"=codereview-taha
```

> This annotation gives you a stable URL: `codereview-taha.westeurope.cloudapp.azure.com`

### 4.2 Install ArgoCD

```bash
kubectl create namespace argocd

helm repo add argo https://argoproj.github.io/argo-helm
helm install argocd argo/argo-cd \
  --namespace argocd \
  --set server.service.type=ClusterIP  # no public exposure
```

### 4.3 Access ArgoCD UI

```bash
# Get initial admin password
kubectl get secret argocd-initial-admin-secret \
  -n argocd \
  -o jsonpath="{.data.password}" | base64 -d

# Open tunnel
kubectl port-forward svc/argocd-server -n argocd 8080:443

# Open in browser: https://localhost:8080
# Username: admin
# Password: from above
```

---

## Phase 5 — Reconnect ArgoCD GitOps Pipeline

### 5.1 Update ArgoCD Application Manifest

Update `app-codereview.yaml` (or equivalent) to point to your AKS cluster:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: codereview
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/Taha7486/Review_Code
    targetRevision: deploy          # your deploy branch
    path: k8s                       # path to manifests
  destination:
    server: https://kubernetes.default.svc
    namespace: default
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

### 5.2 Apply the Application

```bash
kubectl apply -f app-codereview.yaml -n argocd
```

ArgoCD will now watch the `deploy` branch and sync any changes automatically.

---

## Phase 6 — Validate Everything

Run through this checklist after deployment:

```bash
# All pods running?
kubectl get pods

# Services reachable internally?
kubectl get svc

# Ingress has an external IP?
kubectl get ingress

# MySQL PVC bound to Azure disk?
kubectl get pvc

# Secrets populated from Key Vault?
kubectl get secret dotnet-secrets -o yaml

# Prometheus targets up?
kubectl port-forward svc/prometheus -n default 9090:9090
# Open: http://localhost:9090/targets

# Grafana dashboard loaded from ConfigMap?
kubectl port-forward svc/grafana -n default 3000:3000
# Open: http://localhost:3000
# Check dashboard loads automatically (not blank after restart)

# App accessible via Ingress URL?
curl https://codereview-taha.westeurope.cloudapp.azure.com
```

---

## Phase 7 — Credit Management Strategy

AKS nodes are the biggest credit drain (~70% of cost). Use these habits:

### Stop the cluster when not working

```bash
# Stop (preserves config, stops billing for compute)
az aks stop \
  --resource-group rg-codereview \
  --name aks-codereview

# Start again when needed
az aks start \
  --resource-group rg-codereview \
  --name aks-codereview
```

### Scale nodes to 0 for lighter pause

```bash
az aks nodepool scale \
  --resource-group rg-codereview \
  --cluster-name aks-codereview \
  --name default \
  --node-count 0
```

### Full teardown when done for the day (and reprovision with Terraform)

```bash
# Destroy everything
terraform destroy \
  -var="jwt_secret=your_secret" \
  -var="db_password=your_password"

# Recreate next session (~5-10 min)
terraform apply \
  -var="jwt_secret=your_secret" \
  -var="db_password=your_password"
```

> This is actually the best practice to prove your infra is truly reproducible via IaC.

### Set a budget alert

```bash
az consumption budget create \
  --budget-name codereview-budget \
  --amount 50 \
  --time-grain Monthly \
  --start-date 2026-05-01 \
  --end-date 2026-08-01 \
  --resource-group rg-codereview
```

---

## Summary: What This Achieves for Your CV

Before this migration your CV says:
> *"Deployed 7-service microservices to Kubernetes (kind) with ArgoCD and GitOps"*

After this migration it says:
> *"Provisioned AKS cluster on Azure using Terraform; migrated 7-service microservices architecture with ACR image registry, Azure Key Vault secret management via Secrets Store CSI Driver, Nginx Ingress, and ArgoCD GitOps pipeline"*

That is a meaningfully different conversation with any DevOps recruiter.

---

## Quick Reference: Day-by-Day Execution Order

| Day | Tasks |
|---|---|
| **Day 0** (now) | Install tools, write all Terraform files, update manifests, prep secrets list |
| **Day 1** (credits arrive) | `terraform apply`, push images to ACR, install Nginx + ArgoCD |
| **Day 2** | Apply manifests via ArgoCD, validate all services, set budget alert |
| **Ongoing** | Stop cluster when not working, use `terraform destroy/apply` to practice reproducibility |
