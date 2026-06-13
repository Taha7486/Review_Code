# 🏗️ Kubernetes Architecture - Code Review Platform

This document describes the internal networking and orchestration of the platform when running in Kubernetes.

## 📊 Cluster Components

### 1. Application Layer
- **Frontend (react-app)**: 2 replicas. Serves the UI via Nginx.
- **Backend (dotnet-api)**: 2-3 replicas. Orchestrates analysis and auth.
- **Analyzer (php-service)**: 2 replicas. Performs static analysis.

### 2. Data Layer
- **MySQL**: 1 replica (StatefulSet). Stores user data and analysis reports. Uses PersistentVolumeClaims (PVC) for data durability.

### 3. Monitoring Stack
- **Prometheus**: 1 replica. Scrapes metrics from all pods via auto-discovery annotations.
- **Grafana**: 1 replica. Visualizes operational metrics with pre-configured dashboards.

## 🔌 Internal Networking (Service Discovery)

Kubernetes DNS allows services to communicate using their names:

| From | To | Protocol | URL |
| :--- | :--- | :--- | :--- |
| **React App** | **.NET API** | HTTP | `http://dotnet-api:5116` |
| **.NET API** | **PHP Service** | HTTP | `http://php-service:8000` |
| **.NET API** | **MySQL** | TCP | `mysql:3306` |
| **Prometheus** | **All Pods** | HTTP | `[pod-ip]:[port]/metrics` |

## 🔄 GitOps Workflow

The deployment follows the **GitOps** pattern using **ArgoCD**:

1. **Source of Truth**: The `deploy` branch in GitHub contains the current desired state of the cluster (updated by CI).
2. **Reconciliation**: ArgoCD continuously compares the cluster state with the `deploy` branch.
3. **Self-Healing**: If a pod is manually deleted or a configuration is changed via `kubectl`, ArgoCD automatically reverts the change to match Git.
4. **Automated Rollouts**: When CI pushes a new image tag to the `deploy` branch, ArgoCD performs a rolling update in the cluster.

---

## 🔐 Secrets Management

Secrets are stored in **HashiCorp Vault** (standalone server mode, 1 Gi PVC, `vault` namespace) and synced into Kubernetes Secrets by the **Vault Secrets Operator (VSO)**.

| K8s Secret | Vault path | Consumed by |
|---|---|---|
| `dotnet-secrets` | `secret/codereview/dotnet-api/app` | dotnet-api |
| `mysql-secret` | `secret/codereview/dotnet-api/mysql` | dotnet-api, mysql |
| `grafana-secrets` | `secret/codereview/grafana/config` | grafana |

VSO uses `VaultStaticSecret` CRDs (in `k8s/base/vault-static-secrets.yaml`) to declare which Vault paths map to which K8s Secrets. The `VaultAuth` CRs that control Vault authentication are Terraform-managed; Vault ACL policies and auth roles are configured by `scripts/configure-vault.sh`.

Vault auto-unseals on pod restart via a `postStart` lifecycle hook that reads the unseal key from the `vault-unseal-keys` K8s Secret (created once during initial bootstrap).

## 🛡️ Network Policies

All 11 `NetworkPolicy` resources in the `codereview` namespace are Terraform-managed (`terraform/modules/network-policies/`). CNI: Calico v3.29.0.

Default posture: **deny all ingress and egress**, then selectively allow:

| Allowed path | Port |
|---|---|
| Browser → react-app (NodePort) | 80 |
| react-app → dotnet-api | 5116 |
| dotnet-api → php-service | 8000 |
| dotnet-api → mysql | 3306 |
| dotnet-api → GitHub API (egress, non-RFC1918) | 443 |
| prometheus → dotnet-api, php-service | 5116, 8000 |
| grafana → prometheus | 9090 |
| All pods → kube-dns | 53 |

Explicitly blocked: php-service→mysql, php-service→dotnet-api, react-app→php-service, external NodePort access to dotnet-api/prometheus/grafana.

## 🛠️ Infrastructure as Code

- **Base Manifests**: `k8s/base/` (ArgoCD-managed)
- **ArgoCD Config**: `k8s/argocd/`
- **Terraform**: `terraform/environments/kind/` and `terraform/modules/`
- **Cluster Config**: `kind-config.yaml` for local development.
