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

## 🛠️ Infrastructure as Code

- **Base Manifests**: Located in `k8s/base/`
- **ArgoCD Config**: Located in `k8s/argocd/`
- **Cluster Config**: `kind-config.yaml` for local development.
