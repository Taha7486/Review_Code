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

**Step 1 — Create cluster** (PowerShell)
```powershell
kind create cluster --config kind-config.yaml
```

**Step 1b — Install Calico CNI** (PowerShell) — REQUIRED before anything else. `kind-config.yaml` disables the default CNI; without Calico the node stays `NotReady` and every pod stays `Pending`.
```powershell
kubectl apply -f https://raw.githubusercontent.com/projectcalico/calico/v3.29.0/manifests/calico.yaml
kubectl -n kube-system wait pod -l k8s-app=calico-node --for=condition=Ready --timeout=120s
```

**Step 2 — Install ArgoCD** (PowerShell)
```powershell
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
```

**Step 3 — Stage A: provision Vault + VSO Helm releases** (PowerShell)

Two-stage apply is required: Stage A installs the VSO Helm release which registers the `VaultConnection`/`VaultAuth` CRDs. Stage B (after Vault is unsealed) applies everything else — `kubernetes_manifest` resources need those CRDs to exist at plan time.
```powershell
cd terraform/environments/kind
terraform init
terraform apply "-target=module.vault_secrets_operator.helm_release.vso"
```

**Step 3b — Initialize, unseal, and configure Vault** (Git Bash) — run ONCE per cluster creation, BEFORE Stage B.

Vault runs in server mode with a 1 Gi PVC. After this step it auto-unseals on future pod restarts via the `vault-unseal-keys` K8s Secret. Vault policies and auth roles are configured by `scripts/configure-vault.sh` (vault CLI) — Terraform does not manage them.
```bash
export VAULT_ADDR=http://127.0.0.1:30200

# Wait for the container to be running (sealed Vault never passes its readiness
# probe, so --for=condition=ready times out — use jsonpath on the phase instead).
until kubectl get pod vault-0 -n vault -o jsonpath='{.status.phase}' 2>/dev/null | grep -q Running; do
  echo "Waiting for vault-0..."; sleep 5
done

vault operator init -key-shares=1 -key-threshold=1 | tee vault-init.txt
# ⚠ vault-init.txt contains your unseal key and root token.
#   Save both to a password manager NOW, then delete the file.

UNSEAL_KEY=$(grep 'Unseal Key 1:' vault-init.txt | awk '{print $NF}')
export VAULT_TOKEN=$(grep 'Initial Root Token:' vault-init.txt | awk '{print $NF}')

vault operator unseal "$UNSEAL_KEY"
kubectl create secret generic vault-unseal-keys -n vault --from-literal=key="$UNSEAL_KEY"
rm vault-init.txt


cd Desktop/projects/CodeReview/Review_Code
bash scripts/configure-vault.sh
```

**Step 3c — Stage B: full Terraform apply** (PowerShell)
```powershell
terraform apply
```

**Step 4 — Seed Vault secrets** (Git Bash) — generates fresh random values and prints them. Save the output to a password manager.
```bash
bash scripts/seed-vault.sh
# Optional — add a GitHub PAT after seeding:
#   vault kv patch secret/codereview/dotnet-api/app GITHUB_PAT=<your-pat>
```

**Step 5 — Deploy application** (PowerShell)
```powershell
kubectl apply -f k8s/argocd/app-codereview.yaml
```

**Re-seeding after a cluster deletion:** PVC is gone on `kind delete cluster` — repeat steps 1b and 3–4 in full (new `vault operator init`, new secrets). If you only stop/start the kind Docker container without deleting the cluster, the PVC persists and Vault auto-unseals — no re-seeding needed.

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
- Vault auth bindings: two VaultAuth CRs in `codereview` namespace, both pointing to the same VaultConnection. VSO 0.9.x shares one Vault client per `(connection + method + namespace)`, so in practice the `dotnet-api-vault-auth` token is used for all secret reads in the namespace — including `grafana-secrets`. The `codereview-dotnet-api` policy therefore also covers `secret/data/codereview/grafana/*` (see `scripts/configure-vault.sh`).
  - `sa-dotnet-api` → `dotnet-api-vault-auth` → `codereview-dotnet-api` policy → `secret/data/codereview/dotnet-api/*` + `secret/data/codereview/grafana/*`
  - `sa-grafana` → `grafana-vault-auth` → `codereview-grafana` policy → `secret/data/codereview/grafana/*` (role exists; VSO shared-client caveat above applies)
  - `sa-php-service` has no Vault identity; reads `INTERNAL_SERVICE_SECRET` from the `dotnet-secrets` K8s Secret (shared with dotnet-api via `secretKeyRef`)
- For local Docker Compose, secrets come from `.env`.

### Deployment Model (GitOps)
- ArgoCD watches the `deploy` branch. Single application `codereview-app` syncs `k8s/base/` with prune + selfHeal.
- CI pipeline (`.github/workflows/ci.yml`) builds images, scans with Trivy (blocks on CRITICAL), then updates image tags in the `deploy` branch.
- **Terraform owns platform/security resources** (namespaces, ServiceAccounts, RBAC, Vault server, VSO, NetworkPolicies). Vault ACL policies and Kubernetes auth roles are configured by `scripts/configure-vault.sh` (vault CLI) after Vault is initialized. **ArgoCD owns app runtime** (Deployments, Services, ConfigMaps, VaultStaticSecrets). Never add Terraform-owned resource types to `k8s/base/`.
- See `terraform/README.md` for the full ownership table and boundary rule.

### Terraform CI
- `terraform fmt -check` + `terraform init` + `terraform validate` run in CI on `terraform/**` changes (`.github/workflows/terraform-ci.yml`).
- `terraform plan` and `apply` are **manual local operations only** — the kind environment's kubernetes and helm providers require a live cluster endpoint not available to GitHub Actions runners.

### K8s Manifest Layout
- `k8s/base/` — application manifests (ArgoCD-managed)
- `k8s/argocd/` — ArgoCD Application CRs
- `terraform/environments/kind/` — kind cluster environment (single entry point for `terraform apply`)
- `terraform/modules/` — reusable modules: `namespace`, `service-accounts`, `rbac`, `vault`, `vault-secrets-operator`, `network-policies`

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
