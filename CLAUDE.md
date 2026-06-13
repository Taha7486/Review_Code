# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A microservices platform that automates code quality analysis for GitHub repositories. Users connect a GitHub repo; the platform fetches code, runs static analysis (complexity, security, style), and displays metrics on a React dashboard.

## Commands

### Docker Compose (local dev)
```bash
cp .env.example .env   # fill in GITHUB_PAT, JWT_SECRET_KEY, DB credentials
docker-compose up -d --build
# Frontend: http://localhost:3000 | Grafana: http://localhost:3001 | Prometheus: http://localhost:9090
```

### .NET API
```bash
cd dotnet-api
dotnet build -c Release
dotnet test tests/ --configuration Release           # all xUnit integration tests
dotnet test tests/ --filter "ClassName.MethodName"   # run a single test
dotnet run                                            # listens on port 5116
```

EF Core migrations (when changing the schema):
```bash
cd dotnet-api
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### PHP Service
```bash
cd php-service
composer install
php tests/run_tests.php              # custom runner — no PHPUnit
php tests/SecurityAnalyzerTest.php  # run a single test file directly
```

### React Frontend
```bash
cd react-app
npm install
npm test -- --watchAll=false                    # Jest + React Testing Library
npm test -- --watchAll=false --testPathPattern=ComponentName  # single test file
npm run build
npm start                                        # dev server on port 3000
```

### Kubernetes (kind)
```bash
# 1. Create cluster
kind create cluster --config kind-config.yaml

# 1b. Install Calico CNI — REQUIRED before anything else.
#     kind-config.yaml disables the default CNI. Without Calico the node stays
#     NotReady and every pod remains Pending indefinitely.
kubectl apply -f https://raw.githubusercontent.com/projectcalico/calico/v3.29.0/manifests/calico.yaml
kubectl -n kube-system wait pod -l k8s-app=calico-node --for=condition=Ready --timeout=120s

# 2. Install ArgoCD
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# 3. Provision platform layer — two-stage apply:
#    Stage A installs the VSO Helm release (registers VaultConnection/VaultAuth CRDs).
#    Stage B applies everything else (kubernetes_manifest resources need those CRDs to exist at plan time).
#    No Vault provider in Terraform — Vault policies and auth roles are configured via vault CLI in step 3b.
cd terraform/environments/kind
terraform init
terraform apply "-target=module.vault_secrets_operator.helm_release.vso"

# 3b. Initialize, unseal, and configure Vault — run ONCE per cluster creation.
#     Run in Git Bash (not PowerShell). Requires vault CLI in PATH.
#     Must run BEFORE Stage B: the VSO webhook validates VaultConnection/VaultAuth
#     against a live Vault instance, so Vault must be initialized and unsealed first.
#     Vault runs in server mode (persistent PVC); after this step it auto-unseals
#     on future pod restarts via the vault-unseal-keys K8s Secret.
export VAULT_ADDR=http://127.0.0.1:30200
kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=vault -n vault --timeout=120s

vault operator init -key-shares=1 -key-threshold=1 | tee vault-init.txt
# ⚠ vault-init.txt contains your unseal key and root token.
#   Save both to a password manager, then delete the file.

UNSEAL_KEY=$(grep 'Unseal Key 1:' vault-init.txt | awk '{print $NF}')
export VAULT_TOKEN=$(grep 'Initial Root Token:' vault-init.txt | awk '{print $NF}')

vault operator unseal "$UNSEAL_KEY"

# Store unseal key so Vault auto-unseals on future pod restarts:
kubectl create secret generic vault-unseal-keys -n vault --from-literal=key="$UNSEAL_KEY"
rm vault-init.txt   # delete after saving to password manager

# Configure Vault auth methods, policies, and roles:
bash scripts/configure-vault.sh

# 3c. Stage B — apply remaining platform resources (ServiceAccounts, RBAC,
#     NetworkPolicies, VaultConnection, VaultAuth CRs). Vault must be running
#     and unsealed before this step.
terraform apply

# 4. Seed Vault secrets — generates fresh random values and prints them.
#    Save the output to a password manager. Run in Git Bash.
bash scripts/seed-vault.sh
# Optional: if you have a GitHub PAT:
#   vault kv patch secret/codereview/dotnet-api/app GITHUB_PAT=<your-pat>

# 5. Deploy application via ArgoCD
kubectl apply -f k8s/argocd/app-codereview.yaml
```

**Re-seeding after a cluster deletion:** If you delete the kind cluster and recreate it, the PVC is gone — repeat steps 1b and 3–4 in full (new `vault operator init`, new secrets). If you only restart the kind Docker container (stop/start without `kind delete cluster`), the PVC data persists and Vault auto-unseals via the K8s Secret — no re-seeding needed.

## Architecture

### Services

| Service | Stack | Role | Port |
|---------|-------|------|------|
| `react-app` | React 19, Tailwind, nginx | Dashboard UI | 3000 |
| `dotnet-api` | .NET 9, EF Core, Octokit, JWT | API orchestrator + GitHub integration | 5116 |
| `php-service` | PHP 8.2, Slim 4 | Static code analysis engine | 8000 |
| `mysql` | MySQL 8.0 | Persistence | 3306 |
| `prometheus` / `grafana` | — | Observability | 9090 / 3001 |

### Request Flow
```
Browser → React (nginx) → .NET API → GitHub API (fetch code)
                                ↓
                          PHP Service (analysis) → MySQL
```

- React proxies `/api/` to `dotnet-api` (nginx proxy_pass in K8s; `REACT_APP_API_URL` env var in Docker Compose).
- `.NET API` → `php-service` via HTTP POST with `Authorization: Bearer <INTERNAL_SERVICE_SECRET>`.
- `.NET API` exposes `/metrics` for Prometheus scraping; custom metrics include `analysis_duration_seconds` and `vulnerabilities_detected_total`.

### PHP Analysis Modules
Three independent analyzers called by `dotnet-api`:
- **ComplexityAnalyzer** — cyclomatic complexity per function
- **SecurityAnalyzer** — SQL injection, hardcoded secrets, XSS patterns
- **StyleAnalyzer** — snake_case violations, lines >120 chars, nesting depth

### Database Schema
Defined in `infrastructure/mysql/DB_Schema.sql` and applied as a K8s ConfigMap (auto-applied on first Docker Compose container start too). Key tables: `users`, `repositories`, `analysis_runs`, `analysis_issues`, `analysis_metrics` (JSON column).

### Secrets Management (K8s)
- HashiCorp Vault runs in **server mode** (standalone, file storage on a 1 Gi PVC) in the `vault` namespace. Data persists across pod restarts and kind container stop/start. It is lost only on full cluster deletion (`kind delete cluster`), which requires re-running steps 3a and 4 from the bootstrap.
- Vault Secrets Operator (VSO) syncs secrets into K8s via `VaultStaticSecret` CRDs in `k8s/base/vault-static-secrets.yaml`.
- Vault auth bindings (least-privilege):
  - `sa-dotnet-api` → `dotnet-api-vault-auth` → `codereview-dotnet-api` policy → `secret/data/codereview/dotnet-api/*`
  - `sa-grafana` → `grafana-vault-auth` → `codereview-grafana` policy → `secret/data/codereview/grafana/*`
  - `sa-php-service` has no Vault identity; reads `INTERNAL_SERVICE_SECRET` from the `dotnet-secrets` K8s Secret (shared with dotnet-api via `secretKeyRef`)
- For local Docker Compose, secrets come from `.env`.

### Deployment Model (GitOps)
- ArgoCD watches the `deploy` branch. Single application `codereview-app` syncs `k8s/base/` with prune + selfHeal.
- CI pipeline (`.github/workflows/ci.yml`) builds images, scans with Trivy (blocks on CRITICAL), then updates image tags in the `deploy` branch.
- **Terraform owns platform/security resources** (namespaces, ServiceAccounts, RBAC, Vault server + policies + auth bindings, VSO, NetworkPolicies). **ArgoCD owns app runtime** (Deployments, Services, ConfigMaps, VaultStaticSecrets). Never add Terraform-owned resource types to `k8s/base/`.
- See `terraform/README.md` for the full ownership table and boundary rule.

### Terraform CI
- `terraform fmt -check` + `terraform init` + `terraform validate` run in CI on `terraform/**` changes (`.github/workflows/terraform-ci.yml`).
- `terraform plan` and `apply` are **manual local operations only** — the kind environment's kubernetes/helm/vault providers require live endpoints not available to GitHub Actions runners.

### K8s Manifest Layout
- `k8s/base/` — application manifests (ArgoCD-managed)
- `k8s/argocd/` — ArgoCD Application CRs
- `terraform/environments/kind/` — kind cluster environment (single entry point for `terraform apply`)
- `terraform/modules/` — reusable modules: `namespace`, `service-accounts`, `rbac`, `vault`, `vault-policies`, `vault-auth`, `vault-secrets-operator`, `network-policies`

### NetworkPolicies
- All 11 `NetworkPolicy` resources in the `codereview` namespace are Terraform-owned (`terraform/modules/network-policies/main.tf`).
- CNI: Calico v3.29.0 (kindnet disabled). Enforces policies including after kube-proxy NodePort DNAT.
- Allowed traffic: browser→react-app, react-app→dotnet-api, dotnet-api→mysql/php-service/GitHub:443, prometheus→dotnet-api:5116/php-service:8000/k8s-API:443, grafana→prometheus, DNS.
- Denied traffic: php-service→mysql, php-service→dotnet-api, react-app→php-service, mysql→*, external→dotnet-api/prometheus/grafana NodePorts.
- See `terraform/modules/network-policies/README.md` for the full traffic matrix.

## Key Environment Variables
Documented in `.env.example`. Mandatory for local dev: `GITHUB_PAT`, `JWT_SECRET_KEY` (32+ chars), `MYSQL_*`, `INTERNAL_SERVICE_SECRET`, `CORS_ALLOWED_ORIGINS`.

## CI/CD Pipeline
`.github/workflows/ci.yml` runs: test → Trivy scan → build & push Docker images → update deploy branch. All three test suites (dotnet, php, react) run in parallel before any build step.

**Important:** PHP and React test steps use `|| exit 0`, so their failures are non-blocking in CI. Only the .NET test job will fail the pipeline on test errors. Trivy blocks the build on any CRITICAL CVE.

`.github/workflows/terraform-ci.yml` runs on `terraform/**` path changes: `fmt -check` (all modules) → `init` → `validate` (kind environment). `terraform plan` is deferred — requires a live kind cluster and Vault instance not available in GitHub Actions runners.
