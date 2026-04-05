# 🚀 GitOps Implementation Plan - CodeReview Microservices to Kubernetes

> **Target Audience**: Students and developers new to Kubernetes production deployments\
> **Goal**: Migrate from Docker Compose to Kubernetes with ArgoCD for automated GitOps workflows\
> **Cluster**: kind (local Kubernetes in Docker)\
> **Philosophy**: Understanding the **WHY** behind each step, not just copying commands

> 💡 **Note**: This plan is optimized for **kind (local)** clusters. All commands and configurations are kind-specific, with port mappings pre-configured for localhost access.

***

## 📋 Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Prerequisites Checklist](#2-prerequisites-checklist)
3. [Phase 1: Environment Setup](#phase-1-environment-setup-2-3-hours)
4. [Phase 2: Convert Services to Kubernetes](#phase-2-convert-services-to-kubernetes-4-6-hours)
5. [Phase 3: Install and Configure ArgoCD](#phase-3-install-and-configure-argocd-2-3-hours)
6. [Phase 4: Deploy Application](#phase-4-deploy-application-2-4-hours)
7. [Phase 5: Test GitOps Workflow](#phase-5-test-gitops-workflow-1-2-hours)
8. [Phase 6: Documentation and Validation](#phase-6-documentation-and-validation-2-3-hours)
9. [Service Conversion Strategy](#service-conversion-strategy)
10. [Testing Checklist](#testing-checklist)
11. [Troubleshooting Guide](#troubleshooting-guide)

***

## 1. Architecture Overview

### 🔄 What is GitOps?

GitOps is a **declarative** approach to infrastructure and application deployment where:

- **Git is the single source of truth** for your desired system state
- **Automated agents** (like ArgoCD) continuously monitor Git and sync changes to your cluster
- **Changes are auditable** because everything goes through Git commits

### 📊 Current vs. Target Architecture

#### **Current State (Docker Compose)**

```
Developer → docker-compose.yml → Docker Engine → Containers
          ↓
    Manual updates, no audit trail, single-host limitation
```

#### **Target State (GitOps with Kubernetes)**

```
Developer → Git Commit → GitHub Repository
                          ↓
                      ArgoCD (watches repo)
                          ↓
                 Kubernetes Cluster → Pods
                          ↑
                  Auto-sync on Git changes
```

### 🎯 Why This Matters

| Problem with Docker Compose         | GitOps Solution                        |
| ----------------------------------- | -------------------------------------- |
| Manual `docker-compose up` commands | ArgoCD auto-deploys on Git push        |
| No deployment history               | Full Git audit trail                   |
| Single-host limitation              | Multi-node Kubernetes scaling          |
| Difficult rollback                  | One-click rollback to previous commit  |
| No self-healing                     | K8s automatically restarts failed pods |

***

## 2. Prerequisites Checklist

### ✅ Already Installed (You're Ready!)

| Tool           | Purpose                 | Status      |
| -------------- | ----------------------- | ----------- |
| **kubectl**    | Kubernetes CLI          | ✅ Installed |
| **ArgoCD CLI** | ArgoCD management       | ✅ Installed |
| **Docker**     | Build images + Run kind | ✅ Installed |
| **Git**        | Version control         | ✅ Installed |

### 📝 Verify Your Tools

```bash
# Check versions
kubectl version --client
argocd version --client
docker --version
git --version
kind version
```

***

### 📦 Docker Image Requirements

Before migrating, you **must** have Docker images for all services:

| Service        | Existing Dockerfile        | Image Location    | Status       |
| -------------- | -------------------------- | ----------------- | ------------ |
| React Frontend | ✅ `react-app/dockerfile`   | Docker Hub        | Need to push |
| .NET API       | ✅ `dotnet-api/dockerfile`  | Docker Hub        | Need to push |
| PHP Analyzer   | ✅ `php-service/Dockerfile` | Docker Hub        | Need to push |
| MySQL          | ❌ (use official)           | `mysql:8.0`       | Ready        |
| Prometheus     | ❌ (use official)           | `prom/prometheus` | Ready        |
| Grafana        | ❌ (use official)           | `grafana/grafana` | Ready        |

**Action Required:** You already have CI/CD pushing images! Verify they exist:

```bash
# Check if your images exist on Docker Hub
docker pull <your-dockerhub-username>/code-review-api:latest
docker pull <your-dockerhub-username>/code-review-php:latest
docker pull <your-dockerhub-username>/code-review-frontend:latest
```

***

## Phase 1: Environment Setup (✅ COMPLETED)

### 🎯 Goal
Set up a local kind cluster and verify connectivity.

### ⏱️ Actual Time: 30 minutes

***

### Step 1.1: Install kind (if not already done)

Check if kind is installed:

```bash
kind version
```

If not installed:

```powershell
# Windows (PowerShell as Administrator)
choco install kind

# Verify installation
kind version
```

**WHY kind?** It runs Kubernetes inside Docker containers—perfect for local testing without cloud costs.

***

### Step 1.2: Create kind Cluster with Port Mappings

Create a configuration file for your cluster:

```bash
# Navigate to your project
cd c:\Users\pc\Desktop\projects\GitOps-Code_Review\Review_Code

# Create kind config
New-Item -Path "kind-config.yaml" -ItemType File -Force
```

Add this content to `kind-config.yaml`:

```yaml
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
- role: control-plane
  extraPortMappings:
  # React Frontend
  - containerPort: 30000
    hostPort: 3000
    protocol: TCP
  # .NET API
  - containerPort: 30116
    hostPort: 5116
    protocol: TCP
  # Prometheus
  - containerPort: 30090
    hostPort: 9090
    protocol: TCP
  # Grafana
  - containerPort: 30001
    hostPort: 3001
    protocol: TCP
  # ArgoCD UI
  - containerPort: 30080
    hostPort: 8080
    protocol: TCP
```

**🔍 WHY this config?**

- `extraPortMappings` expose services from inside the kind cluster to your Windows host
- Without this, services would only be accessible inside Docker containers
- Port mappings: `containerPort` (inside K8s) → `hostPort` (your localhost)

**Create the cluster:**

```bash
kind create cluster --name codereview --config kind-config.yaml
```

**Expected output:**

```
Creating cluster "codereview" ...
 ✓ Ensuring node image (kindest/node:v1.27.3) 🖼
 ✓ Preparing nodes 📦  
 ✓ Writing configuration 📜 
 ✓ Starting control-plane 🕹️ 
 ✓ Installing CNI 🔌 
 ✓ Installing StorageClass 💾 
Set kubectl context to "kind-codereview"
```

***

### Step 1.3: Verify Cluster Connection

```bash
# Check cluster info
kubectl cluster-info --context kind-codereview

# Verify nodes
kubectl get nodes

# Check system pods
kubectl get pods -n kube-system
```

**✅ Success Criteria:**

- `kubectl get nodes` shows 1 node in `Ready` status
- Control plane URL displayed (<https://127.0.0.1:xxxxx>)
- All kube-system pods in `Running` state

**❌ Common Issues:**

| Error                      | Cause                          | Fix                                                            |
| -------------------------- | ------------------------------ | -------------------------------------------------------------- |
| `kind: command not found`  | Not in PATH                    | Restart terminal or add to PATH manually                       |
| `Port already allocated`   | Another service using the port | Change `hostPort` values in config or stop conflicting service |
| `failed to create cluster` | Docker not running             | Start Docker Desktop                                           |

***

### Step 1.4: Install Metrics Server

The metrics server allows you to use `kubectl top` commands to see resource usage.

```bash
# Download metrics server manifest
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Patch for kind (allows insecure TLS)
kubectl patch deployment metrics-server -n kube-system --type='json' -p='[{"op": "add", "path": "/spec/template/spec/containers/0/args/-", "value": "--kubelet-insecure-tls"}]'

# Wait for metrics server to be ready
kubectl wait --for=condition=available --timeout=60s deployment/metrics-server -n kube-system
```

**WHY?** Metrics Server collects CPU/RAM usage from nodes and pods—useful for debugging resource issues.

**Verify it works:**

```bash
# Wait 30 seconds for metrics to populate, then check
Start-Sleep -Seconds 30
kubectl top nodes
```

**Expected output:**

```
NAME                       CPU(cores)   CPU%   MEMORY(bytes)   MEMORY%   
codereview-control-plane   120m         3%     650Mi           16%
```

**✅ Success Criteria:**

- `kubectl top nodes` shows CPU and memory metrics
- No "metrics not available" error

***

### Step 1.5: Set Default Namespace (Optional)

To avoid typing `-n default` every time:

```bash
kubectl config set-context --current --namespace=default
```

***

## Phase 2: Convert Services to Kubernetes (✅ COMPLETED)

### 🎯 Goal
Transform Docker Compose services into Kubernetes manifests (Deployments, Services, ConfigMaps, Secrets).

### ⏱️ Actual Time: 4 hours

### 📖 Key Concepts

| Docker Compose | Kubernetes Equivalent     | Purpose                                                                |
| -------------- | ------------------------- | ---------------------------------------------------------------------- |
| `services:`    | **Deployment**            | Defines how to run containers (image, replicas, env vars)              |
| `ports:`       | **Service**               | Network endpoint to access pods                                        |
| `environment:` | **ConfigMap/Secret**      | Store configuration (ConfigMap=plain, Secret=sensitive)                |
| `volumes:`     | **PersistentVolumeClaim** | Persistent storage that survives pod restarts                          |
| `depends_on:`  | **N/A**                   | K8s has no built-in dependency—use init containers or readiness probes |

***

### Step 2.1: Create Folder Structure

```bash
cd Review_Code
mkdir -p k8s/{base,overlays/dev}
```

**WHY this structure?**

- `base/`: Core manifests shared across environments
- `overlays/dev/`: Development-specific overrides

**We'll start simple** (no Kustomize yet) and put everything in `k8s/base/`.

***

### Step 2.2: Start with 3 Core Services

We'll deploy in this order (simplest → most complex):

1. **MySQL** (stateful, needs PVC)
2. **PHP Analyzer** (stateless, depends on MySQL)
3. **React Frontend** (stateless, simple)

**WHY this order?**

- MySQL is the foundation (dependencies need it)
- PHP and Frontend are simpler to debug
- .NET API comes later (most complex, depends on all services)

***

### 📝 Service 1: MySQL Database

Create `k8s/base/mysql.yaml`:

```yaml
---
# Persistent storage for MySQL data
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: mysql-pvc
spec:
  accessModes:
    - ReadWriteOnce  # Only one pod can mount (MySQL is single-instance)
  resources:
    requests:
      storage: 5Gi  # 5GB for database
---
# Secret for database credentials
apiVersion: v1
kind: Secret
metadata:
  name: mysql-secret
type: Opaque
stringData:
  MYSQL_ROOT_PASSWORD: "yourSecurePassword123"  # Change this!
  MYSQL_DATABASE: "code_review_tool"
---
# MySQL StatefulSet (ensures stable network identity)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: mysql
spec:
  serviceName: mysql
  replicas: 1  # MySQL doesn't support multiple replicas without clustering
  selector:
    matchLabels:
      app: mysql
  template:
    metadata:
      labels:
        app: mysql
    spec:
      containers:
      - name: mysql
        image: mysql:8.0
        ports:
        - containerPort: 3306
          name: mysql
        env:
        - name: MYSQL_ROOT_PASSWORD
          valueFrom:
            secretKeyRef:
              name: mysql-secret
              key: MYSQL_ROOT_PASSWORD
        - name: MYSQL_DATABASE
          valueFrom:
            secretKeyRef:
              name: mysql-secret
              key: MYSQL_DATABASE
        volumeMounts:
        - name: mysql-data
          mountPath: /var/lib/mysql
        - name: init-schema
          mountPath: /docker-entrypoint-initdb.d
        livenessProbe:  # Restart pod if MySQL crashes
          exec:
            command: ["mysqladmin", "ping", "-h", "localhost"]
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:  # Don't route traffic until MySQL is ready
          exec:
            command: ["mysqladmin", "ping", "-h", "localhost"]
          initialDelaySeconds: 10
          periodSeconds: 5
      volumes:
      - name: mysql-data
        persistentVolumeClaim:
          claimName: mysql-pvc
      - name: init-schema
        configMap:
          name: mysql-init-schema
---
# Service to expose MySQL internally
apiVersion: v1
kind: Service
metadata:
  name: mysql
spec:
  selector:
    app: mysql
  ports:
  - port: 3306
    targetPort: 3306
  clusterIP: None  # Headless service for StatefulSet
---
# ConfigMap for database schema initialization
apiVersion: v1
kind: ConfigMap
metadata:
  name: mysql-init-schema
data:
  schema.sql: |
    -- Your DB schema here (copy from infrastructure/mysql/DB_Schema.sql)
    -- This will run automatically on first startup
```

**🔍 WHY these choices?**

| Choice                                   | Reason                                                             |
| ---------------------------------------- | ------------------------------------------------------------------ |
| **StatefulSet** (not Deployment)         | MySQL needs stable hostname and ordered startup/shutdown           |
| **PersistentVolumeClaim**                | Data must survive pod restarts (otherwise you lose your database!) |
| **Secret** (not ConfigMap)               | Passwords should never be in plaintext                             |
| **Headless Service** (`clusterIP: None`) | StatefulSets need predictable DNS names like `mysql-0.mysql`       |
| **readinessProbe**                       | Prevents traffic before MySQL is ready to accept connections       |

***

### 📝 Service 2: PHP Analyzer

Create `k8s/base/php-service.yaml`:

```yaml
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: php-service
spec:
  replicas: 2  # Run 2 instances for load balancing
  selector:
    matchLabels:
      app: php-service
  template:
    metadata:
      labels:
        app: php-service
    spec:
      containers:
      - name: php
        image: <your-dockerhub-username>/code-review-php:latest
        ports:
        - containerPort: 8000
        env:
        - name: PHP_MEMORY_LIMIT
          value: "512M"
        resources:  # Resource limits prevent one pod from hogging resources
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8000
          initialDelaySeconds: 15
          periodSeconds: 20
        readinessProbe:
          httpGet:
            path: /health
            port: 8000
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: php-service
spec:
  selector:
    app: php-service
  ports:
  - port: 8000
    targetPort: 8000
  type: ClusterIP  # Internal-only (not exposed outside cluster)
```

**🔍 WHY these choices?**

| Choice                           | Reason                                                                       |
| -------------------------------- | ---------------------------------------------------------------------------- |
| **Deployment** (not StatefulSet) | PHP is stateless—pods are interchangeable                                    |
| **2 replicas**                   | Load balancing + high availability (if one crashes, another serves requests) |
| **ClusterIP Service**            | Only .NET API needs to access PHP—no external exposure needed                |
| **Resource limits**              | Prevents memory leaks from crashing the node                                 |

***

### 📝 Service 3: React Frontend

Create `k8s/base/react-app.yaml`:

```yaml
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: react-config
data:
  REACT_APP_API_URL: "http://dotnet-api:5116/api"  # Internal cluster DNS
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: react-app
spec:
  replicas: 2
  selector:
    matchLabels:
      app: react-app
  template:
    metadata:
      labels:
        app: react-app
    spec:
      containers:
      - name: react
        image: <your-dockerhub-username>/code-review-frontend:latest
        ports:
        - containerPort: 80  # Nginx serves on port 80 inside container
        envFrom:
        - configMapRef:
            name: react-config
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
        livenessProbe:
          httpGet:
            path: /
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 15
        readinessProbe:
          httpGet:
            path: /
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: react-app
spec:
  selector:
    app: react-app
  ports:
  - port: 80
    targetPort: 80
    nodePort: 30000  # Exposed on http://localhost:3000 (kind) or node IP (Civo)
  type: NodePort  # Makes service accessible outside cluster
```

**🔍 WHY these choices?**

| Choice                       | Reason                                                                 |
| ---------------------------- | ---------------------------------------------------------------------- |
| **NodePort Service**         | Users need to access frontend from browsers (external access required) |
| **ConfigMap for env vars**   | Frontend needs to know API URL—ConfigMap makes it configurable         |
| **Port 80 inside container** | Your Dockerfile builds with Nginx serving on port 80                   |

***

### Step 2.3: Apply and Verify

```bash
# Apply in dependency order
kubectl apply -f k8s/base/mysql.yaml
kubectl wait --for=condition=ready pod -l app=mysql --timeout=120s

kubectl apply -f k8s/base/php-service.yaml
kubectl wait --for=condition=ready pod -l app=php-service --timeout=60s

kubectl apply -f k8s/base/react-app.yaml
kubectl wait --for=condition=ready pod -l app=react-app --timeout=60s

# Check everything is running
kubectl get pods
kubectl get svc
```

**✅ Success Criteria:**

- All pods show `Running` status with `READY 1/1`
- `kubectl logs <pod-name>` shows no errors
- MySQL pod logs show "ready for connections"

**❌ Common Issues:**

| Error              | Cause                                   | Fix                                                        |
| ------------------ | --------------------------------------- | ---------------------------------------------------------- |
| `ImagePullBackOff` | Image doesn doesn't exist on Docker Hub | Verify image name with `docker pull <image>`               |
| `CrashLoopBackOff` | App crashes on startup                  | Check logs: `kubectl logs <pod-name>`                      |
| `Pending` (PVC)    | No storage provisioner (kind)           | kind has one built-in—wait 30s and check `kubectl get pvc` |

***

### Step 2.4: Test Connectivity

```bash
# Port-forward to test services locally
kubectl port-forward svc/react-app 3000:80

# Open browser to http://localhost:3000
# You should see the React app (might show errors due to missing .NET API)
```

***

## Phase 3: Install and Configure ArgoCD (✅ COMPLETED)

### 🎯 Goal
Install ArgoCD in your kind cluster and configure it to watch your GitHub repository.

### ⏱️ Actual Time: 1 hour

### 📖 What is ArgoCD?

ArgoCD is a **Kubernetes controller** that:

1. **Watches** your Git repository for changes to Kubernetes manifests
2. **Compares** the live cluster state with the desired Git state
3. **Syncs** differences automatically (or manually if you prefer)
4. **Provides a UI** to visualize deployments and history

***

### Step 3.1: Install ArgoCD

```bash
# Create namespace
kubectl create namespace argocd

# Install ArgoCD
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# Wait for pods to start (takes 1-2 minutes)
kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=argocd-server -n argocd --timeout=300s
```

**🔍 WHY a separate namespace?** Keeps ArgoCD isolated from your application.

***

### Step 3.2: Expose ArgoCD UI

For kind, we'll use a NodePort to access the UI on your localhost:

```bash
# Patch ArgoCD server to use NodePort on port 30080
kubectl patch svc argocd-server -n argocd -p '{
  "spec": {
    "type": "NodePort",
    "ports": [
      {
        "name": "http",
        "port": 80,
        "protocol": "TCP",
        "targetPort": 8080,
        "nodePort": 30080
      },
      {
        "name": "https",
        "port": 443,
        "protocol": "TCP",
        "targetPort": 8080,
        "nodePort": 30443
      }
    ]
  }
}'
```

**Access ArgoCD UI:**

- **HTTP**: <http://localhost:8080> (mapped via kind config)
- **HTTPS**: <https://localhost:8080> (self-signed cert, ignore browser warning)

**🔍 WHY NodePort 30080?** This matches the port mapping we defined in `kind-config.yaml`, which forwards `containerPort: 30080` to `hostPort: 8080` on your Windows machine.

***

### Step 3.3: Get Initial Admin Password

```bash
# Get password (Windows PowerShell)
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }

# OR using argocd CLI
argocd admin initial-password -n argocd
```

**Copy the password** that's displayed.

**Login to ArgoCD UI:**

1. Open <http://localhost:8080>
2. Username: `admin`
3. Password: (from command above)
4. Click "Sign In"

**🔒 Security Tip:** Change password immediately:

```bash
# Login via CLI first
argocd login localhost:8080 --insecure

# Update password
argocd account update-password
```

***

### Step 3.4: Connect GitHub Repository

#### Option A: Public Repository (Easier)

```bash
argocd login localhost:8080 --insecure

argocd repo add https://github.com/<your-username>/Review_Code
```

#### Option B: Private Repository

```bash
# Generate a GitHub Personal Access Token
# Go to: Settings → Developer Settings → Personal Access Tokens → Tokens (classic)
# Create new token with scope: repo (full control)

argocd repo add https://github.com/<your-username>/Review_Code \
  --username <your-github-username> \
  --password <your-github-pat>
```

**✅ Verify:**

```bash
argocd repo list
```

Should show `CONNECTION STATUS: Successful`

**Or verify in UI:**

- Go to Settings → Repositories
- Should show your repo with green checkmark

***

### Step 3.5: Create ArgoCD Application

Create `k8s/argocd-app.yaml`:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: codereview-app
  namespace: argocd
spec:
  project: default
  
  # Source: Your GitHub repository
  source:
    repoURL: https://github.com/<your-username>/Review_Code
    targetRevision: HEAD  # Always use latest commit on main branch
    path: k8s/base  # Path to your K8s manifests
  
  # Destination: Your cluster
  destination:
    server: https://kubernetes.default.svc
    namespace: default  # Deploy to default namespace
  
  # Sync policy
  syncPolicy:
    automated:
      prune: true  # Delete resources removed from Git
      selfHeal: true  # Auto-fix manual kubectl changes
      allowEmpty: false
    syncOptions:
    - CreateNamespace=true
    retry:
      limit: 5
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m
```

**🔍 Key Settings Explained:**

| Setting                    | Purpose                                                         |
| -------------------------- | --------------------------------------------------------------- |
| `automated.prune: true`    | If you delete a manifest from Git, ArgoCD deletes it from K8s   |
| `automated.selfHeal: true` | If someone runs `kubectl delete`, ArgoCD recreates the resource |
| `targetRevision: HEAD`     | Always sync from the latest commit                              |
| `retry.limit: 5`           | Retry failed syncs 5 times before giving up                     |

**Apply the application:**

```bash
kubectl apply -f k8s/argocd-app.yaml
```

**✅ Success Criteria:**

- ArgoCD UI shows `codereview-app` with status `Synced` and `Healthy`
- All resources appear in the UI's tree view

***

### Step 3.6: Verify ArgoCD is Watching

```bash
# Check app status
argocd app get codereview-app

# Watch sync in real-time
argocd app wait codereview-app --sync
```

**Expected output:**

```
Health Status:      Healthy
Sync Status:        Synced
```

***

## Phase 4: Deploy Application (✅ COMPLETED)

### 🎯 Goal
Deploy the remaining services (.NET API, Prometheus, Grafana) and configure them properly.

### ⏱️ Actual Time: 3 hours

> 💡 **Lesson Learned (RBAC)**: During this phase, we discovered that Prometheus requires explicit Cluster-level permissions to discover pods. We had to implement a `ClusterRole` and `ServiceAccount` to fix `Forbidden` errors in the scrape logs.

***

### Step 4.1: Deploy .NET API

Create `k8s/base/dotnet-api.yaml`:

```yaml
---
apiVersion: v1
kind: Secret
metadata:
  name: dotnet-secrets
type: Opaque
stringData:
  JWT_SECRET_KEY: "your_super_secret_jwt_key_at_least_32_characters_long"  # Change!
  INTERNAL_SERVICE_SECRET: "shared_secret_key_for_internal_service_auth"  # Change!
  GITHUB_PAT: "your_github_personal_access_token"  # Optional for private repos
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: dotnet-config
data:
  DB_HOST: "mysql"  # DNS name of MySQL service
  DB_PORT: "3306"
  DB_NAME: "code_review_tool"
  DB_USER: "root"
  PHP_ANALYSIS_API_URL: "http://php-service:8000/api/analyze/files"
  ALLOWED_ORIGINS: "http://localhost:3000"  # Update for Civo external IP
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: dotnet-api
  template:
    metadata:
      labels:
        app: dotnet-api
    spec:
      initContainers:  # Wait for MySQL before starting
      - name: wait-for-mysql
        image: busybox:1.28
        command: ['sh', '-c', 'until nc -z mysql 3306; do echo waiting for mysql; sleep 2; done;']
      containers:
      - name: dotnet
        image: <your-dockerhub-username>/code-review-api:latest
        ports:
        - containerPort: 5116
        env:
        - name: ASPNETCORE_URLS
          value: "http://+:5116"
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: mysql-secret
              key: MYSQL_ROOT_PASSWORD
        envFrom:
        - configMapRef:
            name: dotnet-config
        - secretRef:
            name: dotnet-secrets
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5116
          initialDelaySeconds: 30
          periodSeconds: 20
        readinessProbe:
          httpGet:
            path: /health
            port: 5116
          initialDelaySeconds: 15
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: dotnet-api
spec:
  selector:
    app: dotnet-api
  ports:
  - port: 5116
    targetPort: 5116
    nodePort: 30116  # Expose for external access
  type: NodePort
```

**🔍 WHY initContainer?**

- Without it, the .NET API would crash trying to connect to MySQL before it's ready
- `initContainers` run **before** the main container and must complete successfully

***

### Step 4.2: Deploy Monitoring Stack

Create `k8s/base/monitoring.yaml`:

```yaml
---
# Prometheus ConfigMap
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
    scrape_configs:
      - job_name: 'kubernetes-pods'
        kubernetes_sd_configs:
        - role: pod
        relabel_configs:
        - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
          action: keep
          regex: true
        - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
          action: replace
          target_label: __metrics_path__
          regex: (.+)
        - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_port]
          action: replace
          target_label: __address__
          regex: ([^:]+)(?::\d+)?;(\d+)
          replacement: $1:$2
---
# Prometheus Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prometheus
spec:
  replicas: 1
  selector:
    matchLabels:
      app: prometheus
  template:
    metadata:
      labels:
        app: prometheus
    spec:
      containers:
      - name: prometheus
        image: prom/prometheus:latest
        args:
        - '--config.file=/etc/prometheus/prometheus.yml'
        - '--storage.tsdb.path=/prometheus'
        ports:
        - containerPort: 9090
        volumeMounts:
        - name: config
          mountPath: /etc/prometheus
        - name: data
          mountPath: /prometheus
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
      volumes:
      - name: config
        configMap:
          name: prometheus-config
      - name: data
        emptyDir: {}  # Temporary storage (data lost on pod restart)
---
apiVersion: v1
kind: Service
metadata:
  name: prometheus
spec:
  selector:
    app: prometheus
  ports:
  - port: 9090
    targetPort: 9090
    nodePort: 30090
  type: NodePort
---
# Grafana Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: grafana
spec:
  replicas: 1
  selector:
    matchLabels:
      app: grafana
  template:
    metadata:
      labels:
        app: grafana
    spec:
      containers:
      - name: grafana
        image: grafana/grafana:latest
        env:
        - name: GF_SECURITY_ADMIN_PASSWORD
          value: "admin"  # Change in production!
        - name: GF_USERS_ALLOW_SIGN_UP
          value: "false"
        ports:
        - containerPort: 3000
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "250m"
---
apiVersion: v1
kind: Service
metadata:
  name: grafana
spec:
  selector:
    app: grafana
  ports:
  - port: 3000
    targetPort: 3000
    nodePort: 30001
  type: NodePort
```

***

### Step 4.3: Commit and Push to GitHub

```bash
cd Review_Code
git add k8s/
git commit -m "feat: Add Kubernetes manifests for GitOps deployment"
git push origin main
```

**🎯 This is where GitOps magic happens!**

***

### Step 4.4: Watch ArgoCD Auto-Deploy

```bash
# Watch ArgoCD detect changes and sync
argocd app get codereview-app --refresh

# Stream logs
kubectl get pods -w
```

**✅ Success Criteria:**

- ArgoCD UI shows new resources appearing
- All pods reach `Running` state
- Applications are accessible at configured NodePorts

***

### Step 4.5: Verify All Services

```bash
# Check pod status
kubectl get pods

# Check services
kubectl get svc

# Test each service
# Frontend
curl http://localhost:3000  # kind
curl http://<node-ip>:30000  # Civo

# API
curl http://localhost:5116/health

# Prometheus
curl http://localhost:9090/-/healthy

# Grafana
curl http://localhost:3001/api/health
```

***

## Phase 5: Test GitOps Workflow (✅ COMPLETED)

### 🎯 Goal
Verify that changes pushed to Git automatically deploy to Kubernetes.

### ⏱️ Actual Time: 2 hours

> 💡 **Validation**: Successfully performed Zero-Downtime scaling, Self-Healing (deleting pods), and a full **Git Rollback** using `git revert` after a simulated configuration error.

***

### Test 1: Update React Frontend Replicas

```bash
# Edit k8s/base/react-app.yaml
# Change: replicas: 2 → replicas: 3

git add k8s/base/react-app.yaml
git commit -m "scale: Increase React replicas to 3"
git push origin main
```

**Watch ArgoCD:**

```bash
argocd app get codereview-app --refresh
kubectl get pods -l app=react-app -w
```

**✅ Success Criteria:**

- Within 3 minutes (default sync interval), ArgoCD detects change
- UI shows "OutOfSync" → "Syncing" → "Synced"
- `kubectl get pods` shows 3 React pods

***

### Test 2: Update Environment Variable

```bash
# Edit k8s/base/dotnet-api.yaml
# Change ConfigMap: ALLOWED_ORIGINS: "http://newdomain.com"

git add k8s/base/dotnet-api.yaml
git commit -m "config: Update CORS origins"
git push origin main
```

**Verify:**

```bash
# Watch deployment rollout
kubectl rollout status deployment/dotnet-api

# Check new env var
kubectl exec -it deployment/dotnet-api -- env | grep ALLOWED_ORIGINS
```

**✅ Success Criteria:**

- Deployment performs rolling update (zero downtime)
- New pods have updated environment variables

***

### Test 3: Demonstrate Rollback

```bash
# Intentionally break the .NET API
# Edit k8s/base/dotnet-api.yaml
# Change: image: <username>/code-review-api:latest → image: <username>/code-review-api:broken

git add k8s/base/dotnet-api.yaml
git commit -m "bug: Broken image for rollback demo"
git push origin main
```

**Wait for failure:**

```bash
kubectl get pods -l app=dotnet-api -w
# Pods will show ImagePullBackOff
```

**Rollback via Git:**

```bash
git revert HEAD  # Undo last commit
git push origin main
```

**✅ Success Criteria:**

- ArgoCD detects revert and syncs previous working state
- Pods recover within 2-3 minutes

***

### Test 4: Self-Healing Demo

```bash
# Manually delete a pod
kubectl delete pod -l app=php-service --force

# Watch ArgoCD recreate it
kubectl get pods -l app=php-service -w
```

**✅ Success Criteria:**

- New pod appears within 10 seconds (due to `selfHeal: true`)
- ArgoCD UI shows brief "OutOfSync" then "Synced"

***

### GitOps Features

- ✅ **Automated Deployments**: Git push triggers automatic sync to cluster
- ✅ **Self-Healing**: Manual changes auto-reverted to Git state
- ✅ **One-Click Rollback**: Revert Git commit to undo deployments
- ✅ **Multi-Environment Ready**: Kustomize overlays for dev/staging/prod

### Architecture

See [K8s Architecture Docs](docs/k8s-architecture.md) for detailed diagrams.


## Service Conversion Strategy

### 🎯 Recommended Deployment Order

| Order | Service | Complexity | Dependencies | Rationale |
|---|---|---|---|---|
| 1 | MySQL | Medium | None | Foundation for all services |
| 2 | PHP Analyzer | Low | MySQL (optional) | Stateless, easy to debug |
| 3 | React Frontend | Low | None (can mock API) | UI-only, no backend deps |
| 4 | .NET API | High | MySQL, PHP | Complex config, depends on all |
| 5 | Prometheus | Low | None | Monitoring foundation |
| 6 | Grafana | Low | Prometheus | Visualization layer |

---

### Handling Stateful Services (MySQL)

**Challenge:** MySQL stores data that must persist across pod restarts.

**Solutions:**

| Approach | Use Case | Implementation |
|---|---|---|
| **PersistentVolumeClaim** | Single-instance MySQL | ✅ Implemented in Phase 2 |
| **StatefulSet** | Ordered deployment, stable hostnames | ✅ Implemented in Phase 2 |
| **Managed Database** (AWS RDS, Civo DB) | Production workloads | Use for real production |
| **Backup CronJob** | Disaster recovery | Add in Phase 7 (advanced) |

**Important Notes:**
- **Do NOT use `emptyDir`** for MySQL—data is lost on pod restart!
- **Always test** by deleting MySQL pod and verifying data survives
- **kind users**: Your PVC uses local storage (disappears when cluster is deleted)
- **Civo users**: Your PVC uses Civo Block Storage (persists, billed separately)

---

### Environment Variables and Secrets

**Decision Tree:**

```

Is the value sensitive (password, API key)?
├─ YES → Use Secret
│         apiVersion: v1
│         kind: Secret
│         type: Opaque
│         stringData:
│           KEY: "value"
│
└─ NO → Is it environment-specific (URLs, replicas)?
├─ YES → Use Kustomize overlay
│         # overlays/dev/kustomization.yaml
│         patches:
│         - target:
│             kind: ConfigMap
│             name: myconfig
│           patch: |-
│             - op: replace
│               path: /data/API\_URL
│               value: "<http://dev-api:5116>"
│
└─ NO → Use ConfigMap


````

**Best Practices:**
- ❌ **Never commit secrets to Git** (use sealed-secrets or external secret operators)
- ✅ **Use placeholder secrets in Git** with a README for local overrides
- ✅ **For production**: Use [External Secrets Operator](https://external-secrets.io/) with AWS Secrets Manager/Azure Key Vault

---

## Testing Checklist

### ✅ Phase 2: Service Deployment

- [ ] All pods in `Running` state (`kubectl get pods`)
- [ ] No `CrashLoopBackOff` or `ImagePullBackOff` errors
- [ ] MySQL pod logs show "ready for connections"
- [ ] PHP service responds to `curl http://php-service:8000/health` (from another pod)
- [ ] React app accessible at `http://localhost:3000` (or NodePort)

**Debugging Commands:**
```bash
# Check pod status
kubectl get pods -o wide

# View pod logs
kubectl logs <pod-name>

# Describe pod (shows events)
kubectl describe pod <pod-name>

# Shell into pod
kubectl exec -it <pod-name> -- /bin/sh

# Test internal DNS
kubectl run test --image=busybox --rm -it -- nslookup mysql
````

***

### ✅ Phase 3: ArgoCD Installation

- [ ] ArgoCD UI accessible (localhost:8080 or external IP)
- [ ] Can login with admin credentials
- [ ] Repository shows "Connection Successful"
- [ ] `codereview-app` appears in application list
- [ ] Initial sync completes without errors

**Debugging Commands:**

```bash
# Check ArgoCD pods
kubectl get pods -n argocd

# View ArgoCD logs
kubectl logs -n argocd deployment/argocd-server

# Check application status
argocd app get codereview-app

# Force sync
argocd app sync codereview-app
```

***

### ✅ Phase 5: GitOps Workflow

- [ ] **Auto-Sync Test**: Git push triggers deployment within 3 minutes
- [ ] **Self-Heal Test**: Deleted pod recreates automatically
- [ ] **Rollback Test**: Git revert restores previous state
- [ ] **Config Change Test**: ConfigMap update triggers pod restart

**Validation:**

```bash
# Verify auto-sync is enabled
kubectl get application codereview-app -n argocd -o yaml | grep automated

# Check sync status
argocd app wait codereview-app --sync --health --timeout 300

# Verify self-heal recreated deleted resource
kubectl get events --sort-by='.metadata.creationTimestamp' | grep <deleted-pod-name>
```

## 🎓 Summary: What You've Learned

By completing this plan, you've gained hands-on experience with:

✅ **Kubernetes Fundamentals**

- Deployments, Services, ConfigMaps, Secrets, PersistentVolumeClaims
- Pod lifecycle management (liveness/readiness probes)
- Resource limits and requests

✅ **GitOps Methodology**

- Declarative infrastructure as code
- Automated deployments via Git commits
- Self-healing and drift detection

✅ **Production Skills**

- Stateful vs. stateless workloads
- Service discovery and DNS
- Rolling updates and rollbacks
- Observability (Prometheus + Grafana)

✅ **Real-World DevOps**

- CI/CD integration (GitHub Actions → Docker Hub → K8s)
- Multi-service orchestration
- Debugging distributed systems

***

### 💡 Note on Monitoring Persistence

Currently, the monitoring stack (Prometheus and Grafana) uses `emptyDir` for storage. This means all historical metrics and custom dashboard changes will be lost if the pods restart or the cluster is recreated. To fix this, you should later implement **PersistentVolumeClaims (PVCs)** for both services, similar to how the MySQL database is configured.
