# Terraform Platform Layer

Terraform owns cluster and security resources that must exist before ArgoCD syncs the application. ArgoCD owns application workloads only.

## Ownership Boundary

### Terraform owns (platform / security layer)

| Resource type | Module | Namespace |
|---|---|---|
| Namespaces (`codereview`, `vault`) | `namespace` | — |
| ServiceAccounts (sa-react-app, sa-dotnet-api, sa-php-service, sa-mysql, sa-grafana, prometheus) | `service-accounts` | `codereview` |
| Prometheus ClusterRole + ClusterRoleBinding | `rbac/prometheus` | cluster-scoped |
| Vault dev server (Helm release) | `vault` | `vault` |
| Vault Secrets Operator (Helm release) | `vault-secrets-operator` | `vault` |
| VaultConnection CR | `vault-secrets-operator` | `codereview` |
| Vault Kubernetes auth backend + config | `vault-auth` | Vault |
| Vault auth roles (`dotnet-api-role`, `grafana-role`) | `vault-auth` | Vault |
| Vault policies (`codereview-dotnet-api`, `codereview-grafana`) | `vault-policies` | Vault |
| VaultAuth CRs (`dotnet-api-vault-auth`, `grafana-vault-auth`) | `vault-secrets-operator` | `codereview` |
| NetworkPolicies (11 policies, default-deny baseline) | `network-policies` | `codereview` |

### ArgoCD owns (application / runtime layer)

ArgoCD application `codereview-app` syncs `k8s/base/` from the `deploy` branch with `prune: true` and `selfHeal: true`.

| Resource type | Examples |
|---|---|
| Deployments and StatefulSets | react-app, dotnet-api, php-service, mysql, prometheus, grafana |
| Services | All app services |
| ConfigMaps (non-secret) | nginx config, grafana dashboards, mysql schema init |
| VaultStaticSecrets | dotnet-secrets, mysql-secret, grafana-secrets |
| HorizontalPodAutoscalers | dotnet-api, php-service |

### Boundary rule

> **Terraform manages platform and security resources. ArgoCD manages application runtime resources. Never add Terraform-owned resource types (Namespaces, ServiceAccounts, RBAC, NetworkPolicies, VaultAuth CRs) to `k8s/base/`.**

The separation exists to prevent sync wars: ArgoCD's prune will delete anything in `k8s/base/` that is not in the deploy branch. If Terraform-owned resources leak into that path, ArgoCD will delete them on the next sync. Terraform resources carry the label `app.kubernetes.io/managed-by: terraform`; ArgoCD resources carry the ArgoCD tracking annotation. These must never appear on the same object.

See operational log in `docs/infrastructure-hardening-plan.md` (Issue: bootstrap/ArgoCD-Terraform conflict) for a real incident where this boundary was violated and the mitigation applied.

## CI Workflow

`.github/workflows/terraform-ci.yml` runs on pull requests and pushes to `main` when `terraform/**` paths change:

| Step | Runs in CI | Notes |
|---|---|---|
| `terraform fmt -check -recursive` | Yes | Fails the build if any `.tf` file is not formatted |
| `terraform init` | Yes | Downloads providers using the committed `.terraform.lock.hcl` |
| `terraform validate` | Yes | Validates schema and cross-references; no live connectivity needed |
| `terraform plan` | **No** | Requires live providers — see below |
| `terraform apply` | **No** | Manual local operation only |

### Why plan/apply are not in CI

The kind environment uses three providers that all require live endpoints at plan time:

- `hashicorp/kubernetes` — needs a reachable kubeconfig (kind cluster)
- `hashicorp/helm` — needs the same Kubernetes API
- `hashicorp/vault` — needs a running Vault instance (NodePort 30200)

GitHub Actions runners have none of these. Exposing the local kind cluster to CI is not viable. A remote environment (managed Kubernetes + Vault) would enable plan/apply in CI, but that is out of scope for this kind-based setup.

**Consequence:** `terraform plan` must be run locally and reviewed before every `terraform apply`. This is a structural property of the kind environment, not a deferred TODO.

If a non-kind environment is introduced in the future, this document and the CI workflow should be updated to add plan (and optionally apply on a protected branch).

## Local kind Environment

### First apply (new cluster)

Vault CRDs from the VSO Helm release must exist before `kubernetes_manifest` resources referencing them are created. Apply in stages to avoid a chicken-and-egg plan failure:

```bash
cd terraform/environments/kind

# Stage 1: namespaces, SAs, RBAC, Vault server, Vault policies/auth
terraform init
terraform apply -target=module.namespaces \
                -target=module.service_accounts \
                -target=module.prometheus_rbac \
                -target=module.vault \
                -target=module.vault_policies \
                -target=module.vault_auth \
                -target=module.vault_secrets_operator.helm_release.vso

# Stage 2: VaultConnection + VaultAuth CRs (CRDs now exist), NetworkPolicies
terraform apply
```

### Subsequent applies (cluster already running)

```bash
cd terraform/environments/kind
terraform plan   # review before applying
terraform apply
```

### Seed Vault secrets (after every cluster restart)

Vault runs in dev mode and loses all KV data on pod restart. Re-seed after each cluster start:

```bash
export VAULT_ADDR=http://127.0.0.1:30200 VAULT_TOKEN=root
bash scripts/seed-vault.sh
```

VSO then materialises three Kubernetes Secrets in the `codereview` namespace:

| K8s Secret | Vault path | VaultAuth used |
|---|---|---|
| `dotnet-secrets` | `secret/codereview/dotnet-api/app` | `dotnet-api-vault-auth` |
| `mysql-secret` | `secret/codereview/dotnet-api/mysql` | `dotnet-api-vault-auth` |
| `grafana-secrets` | `secret/codereview/grafana/config` | `grafana-vault-auth` |

Rotate all placeholder values from `seed-vault.sh` before using this outside local development.

## Module Reference

| Module path | What it creates |
|---|---|
| `modules/namespace` | A single Kubernetes namespace with standard labels |
| `modules/service-accounts` | One or more ServiceAccounts from a map input |
| `modules/rbac/prometheus` | ClusterRole + ClusterRoleBinding for Prometheus pod discovery |
| `modules/vault` | Vault dev server via Helm, NodePort 30200 |
| `modules/vault-policies` | Vault ACL policies (`codereview-dotnet-api`, `codereview-grafana`) |
| `modules/vault-auth` | Vault Kubernetes auth backend + roles |
| `modules/vault-secrets-operator` | VSO Helm release, VaultConnection CR, VaultAuth CRs |
| `modules/network-policies` | 11 NetworkPolicies: default-deny baseline + per-service allow rules |

## Production Environment

`terraform/environments/prod` is a placeholder. No production target has been selected. When one is:

- Configure a remote backend with state locking before using Terraform for shared infrastructure.
- Add platform-specific provider configuration.
- Revisit the CI workflow — plan and apply in CI become viable once provider endpoints are stable and accessible.
- Do not commit production Terraform state or secrets to this directory.
