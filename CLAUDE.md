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
dotnet test tests/ --configuration Release     # xUnit integration tests
dotnet run                                      # listens on port 5116
```

### PHP Service
```bash
cd php-service
composer install
php tests/run_tests.php                         # custom test runner (no PHPUnit)
```

### React Frontend
```bash
cd react-app
npm install
npm test -- --watchAll=false                   # Jest + React Testing Library
npm run build
npm start                                       # dev server on port 3000
```

### Kubernetes (kind)
```bash
# 1. Create cluster
kind create cluster --config kind-config.yaml

# 2. Install ArgoCD
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# 3. Provision platform layer (Vault, RBAC, namespaces)
cd terraform/environments/kind && terraform init && terraform apply

# 4. Seed Vault secrets
export VAULT_ADDR=http://127.0.0.1:30200 VAULT_TOKEN=root
bash ../../scripts/seed-vault.sh

# 5. Deploy application via ArgoCD
kubectl apply -f k8s/argocd/app-codereview.yaml
```

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

- React proxies `/api/` to `dotnet-api` (nginx proxy_pass in K8s; env var `REACT_APP_API_URL` in Docker Compose).
- `.NET API` → `php-service` via HTTP POST with `Authorization: Bearer <INTERNAL_SERVICE_SECRET>`.
- `.NET API` exposes `/metrics` for Prometheus scraping.

### Database Schema
Defined in `infrastructure/mysql/DB_Schema.sql` and applied as a K8s ConfigMap. Key tables: `users`, `repositories`, `analysis_runs`, `analysis_issues`, `analysis_metrics` (JSON).

### Secrets Management (K8s)
- HashiCorp Vault runs in the `vault` namespace, provisioned by Terraform.
- Vault Secrets Operator (VSO) syncs secrets into K8s via `VaultStaticSecret` CRDs in `k8s/base/vault-static-secrets.yaml`.
- Vault auth bindings (least-privilege, Step 4.11 complete):
  - `sa-dotnet-api` → `dotnet-api-vault-auth` → `codereview-dotnet-api` policy → `secret/data/codereview/dotnet-api/*`
  - `sa-grafana` → `grafana-vault-auth` → `codereview-grafana` policy → `secret/data/codereview/grafana/*`
  - `sa-php-service` has no Vault identity; it reads `INTERNAL_SERVICE_SECRET` from the `dotnet-secrets` K8s Secret (shared with dotnet-api via `secretKeyRef`)
- For local Docker Compose, secrets come from `.env`.

### Deployment Model (GitOps)
- ArgoCD watches the `deploy` branch of this repo.
- CI pipeline (`.github/workflows/ci.yml`) builds images, scans with Trivy (blocks on CRITICAL), then updates image tags in the `deploy` branch.
- Terraform manages the platform layer (namespaces, RBAC, Vault); ArgoCD manages the application layer (`k8s/base/`).

### K8s Manifest Layout
- `k8s/base/` — application manifests (ArgoCD-managed)
- `k8s/platform/` — platform manifests (Terraform-managed)
- `k8s/argocd/` — ArgoCD Application CRs
- `terraform/modules/` — reusable modules: `namespace`, `service-accounts`, `rbac`, `vault`, `vault-policies`, `vault-auth`, `vault-secrets-operator`, `network-policies`

### NetworkPolicies (Area 3 — IMPLEMENTED 2026-06-12)
- All 11 `NetworkPolicy` resources in the `codereview` namespace are Terraform-owned (`terraform/modules/network-policies/main.tf`).
- CNI: Calico v3.29.0 (kindnet disabled). Enforces policies including after kube-proxy NodePort DNAT.
- Applied atomically (default-deny + DNS + all app policies in one `terraform apply`).
- Allowed traffic: browser→react-app, react-app→dotnet-api, dotnet-api→mysql/php-service/GitHub:443, prometheus→dotnet-api:5116/php-service:8000/k8s-API:443, grafana→prometheus, DNS.
- Denied traffic: php-service→mysql, php-service→dotnet-api, react-app→php-service, mysql→*, external→dotnet-api/prometheus/grafana NodePorts.
- See `terraform/modules/network-policies/README.md` for full traffic matrix and `phase3_imp.md` for conformance test results.

## Key Environment Variables
Documented in `.env.example`. Mandatory for local dev: `GITHUB_PAT`, `JWT_SECRET_KEY`, `MYSQL_*`, `INTERNAL_SERVICE_SECRET`, `CORS_ALLOWED_ORIGINS`.

## CI/CD Pipeline
`.github/workflows/ci.yml` runs: test → Trivy scan → build & push Docker images → update deploy branch. All three test suites (dotnet, php, react) run in parallel before any build step.
