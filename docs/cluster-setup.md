# Kubernetes Cluster Setup — Kind (Local Dev)

All commands below assume your working directory is the **repo root** (where you cloned the project). No `cd` into subdirectories is needed — Terraform commands use `-chdir` and all paths are relative to the repo root.

---

## Prerequisites

| Tool | Purpose | Install |
|------|---------|---------|
| Docker Desktop | Runs kind nodes as containers | [docs.docker.com](https://docs.docker.com/get-docker/) |
| kind | Local Kubernetes in Docker | `choco install kind` or [kind.sigs.k8s.io](https://kind.sigs.k8s.io/) |
| kubectl | Kubernetes CLI | `choco install kubernetes-cli` |
| Terraform ≥ 1.6 | Provisions platform resources | `choco install terraform` |
| Helm | Used by Terraform Helm provider | `choco install kubernetes-helm` |
| vault CLI | Vault init/unseal/seed | [vaultproject.io/downloads](https://www.vaultproject.io/downloads) |
| Git Bash | Required for the Vault init step | Included with Git for Windows |

---

## Step 1 — Create the kind cluster

```powershell
kind create cluster --config kind-config.yaml
```

## Step 1b — Install Calico CNI

`kind-config.yaml` disables the default CNI (kindnet). Without Calico the node stays `NotReady` and every pod stays `Pending`. Run this immediately after cluster creation.

```powershell
kubectl apply -f https://raw.githubusercontent.com/projectcalico/calico/v3.29.0/manifests/calico.yaml
kubectl -n kube-system wait pod -l k8s-app=calico-node --for=condition=Ready --timeout=120s
```

## Step 2 — Install ArgoCD

```powershell
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
kubectl apply -f k8s/argocd/argocd-ui.yaml
```

The last command creates the `argocd-server-nodeport` Service that exposes the UI — it is not included in the upstream install manifest.

## Step 3 — Stage A: provision Vault + VSO

Two-stage Terraform apply is required. Stage A installs the VSO Helm release, which registers the `VaultConnection`/`VaultAuth` CRDs. Stage B (after Vault is unsealed) applies everything else — `kubernetes_manifest` resources need those CRDs to exist at plan time.

```powershell
terraform -chdir=terraform/environments/kind init
terraform -chdir=terraform/environments/kind apply "-target=module.vault_secrets_operator.helm_release.vso"
```

## Step 3b — Initialize, unseal, and configure Vault

Run in **Git Bash**. Do this **once per cluster creation**, before Stage B.

Open Git Bash **inside the repo folder** (right-click the cloned folder → "Open Git Bash here"), or `cd` to it manually. All `bash scripts/` calls are relative to the repo root.

```bash
export VAULT_ADDR=http://127.0.0.1:30200

# Sealed Vault never passes its readiness probe — poll phase instead.
until kubectl get pod vault-0 -n vault -o jsonpath='{.status.phase}' 2>/dev/null | grep -q Running; do
  echo "Waiting for vault-0..."; sleep 5
done

vault operator init -key-shares=1 -key-threshold=1 | tee vault-init.txt
```

> **vault-init.txt contains your unseal key and root token. Save both to a password manager NOW, then delete the file.**

```bash
UNSEAL_KEY=$(grep 'Unseal Key 1:' vault-init.txt | awk '{print $NF}')
export VAULT_TOKEN=$(grep 'Initial Root Token:' vault-init.txt | awk '{print $NF}')

vault operator unseal "$UNSEAL_KEY"
kubectl create secret generic vault-unseal-keys -n vault --from-literal=key="$UNSEAL_KEY"
rm vault-init.txt

bash scripts/configure-vault.sh
```

## Step 3c — Stage B: full Terraform apply

```powershell
terraform -chdir=terraform/environments/kind apply
```

## Step 4 — Seed Vault secrets

Run in **Git Bash** from the repo root. `VAULT_ADDR` and `VAULT_TOKEN` must still be exported from Step 3b (or re-export them).

```bash
bash scripts/seed-vault.sh
```

The script generates fresh random values for all secrets and prints them. Save the output to a password manager.

Optionally add your GitHub PAT now (the app works without one, but GitHub-dependent analysis endpoints will return 401):

```bash
vault kv patch secret/codereview/dotnet-api/app GITHUB_PAT=<your-pat>
```

## Step 5 — Deploy the application

```powershell
kubectl apply -f k8s/argocd/app-codereview.yaml
```

ArgoCD will pull `k8s/base/` from the `deploy` branch and sync all application resources.

## Step 6 — Verify and access

```powershell
kubectl get pods -n codereview   # all 10 pods should reach 1/1 Running within ~2 min
```

### Browser access (no port-forward needed — kind maps host ports directly)

| Service | URL | Credentials |
|---------|-----|-------------|
| React UI | http://localhost:3000 | Register a new account (no pre-seeded users) |
| Grafana | http://localhost:3001 | `admin` / see command below |
| Prometheus | http://localhost:9090 | — |
| ArgoCD | https://localhost:8080 | `admin` / see command below (accept self-signed cert) |
| Vault UI | http://localhost:30200 | Root token from Step 3b |

**Get Grafana password** (Git Bash):
```bash
kubectl get secret grafana-secrets -n codereview -o jsonpath='{.data.admin-password}' | base64 -d
```

**Get ArgoCD password** (Git Bash):
```bash
kubectl get secret argocd-initial-admin-secret -n argocd -o jsonpath='{.data.password}' | base64 -d
```

---

## Daily operations

### Start the cluster (after Docker Desktop restarts)

If you stopped Docker Desktop without running `kind delete cluster`, the PVC and Vault data are intact. Vault auto-unseals via the `postStart` hook — no manual action needed.

```powershell
# Verify everything is back up
kubectl get pods -n codereview
kubectl get pod vault-0 -n vault
```

### Add or update a GitHub PAT

```bash
vault kv patch secret/codereview/dotnet-api/app GITHUB_PAT=<your-pat>
kubectl rollout restart deployment/dotnet-api -n codereview
```

### Force-restart Prometheus or Grafana after ConfigMap changes

```powershell
kubectl rollout restart deployment/prometheus -n codereview
kubectl rollout restart deployment/grafana -n codereview
```

---

## Teardown and re-bootstrap

### Full cluster deletion

```powershell
kind delete cluster
```

All PVCs (including Vault's 1 Gi storage) are destroyed. On next bootstrap, repeat Steps 1b and 3–4 in full (`vault operator init` creates a new root token and unseal key; `seed-vault.sh` writes fresh secrets).

### Cleanup without cluster deletion

To free Docker resources without losing Vault data, stop the kind Docker container instead of deleting the cluster:

```powershell
docker stop kind-control-plane   # stops cluster, preserves PVC
docker start kind-control-plane  # resumes; Vault auto-unseals
```

---

## Development commands (without Kubernetes)

### Docker Compose (quickest local run)

```bash
cp .env.example .env   # fill in GITHUB_PAT, JWT_SECRET_KEY, DB credentials
docker-compose up -d --build
# Frontend: http://localhost:3000 | Grafana: http://localhost:3001 | Prometheus: http://localhost:9090
```

### .NET API

```bash
cd dotnet-api
dotnet build -c Release
dotnet test tests/ --configuration Release
dotnet run   # listens on port 5116
```

EF Core migrations:

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### PHP Service

```bash
cd php-service
composer install
php tests/run_tests.php
```

### React Frontend

```bash
cd react-app
npm install
npm test -- --watchAll=false
npm start   # dev server on port 3000
```
