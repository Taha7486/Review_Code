# Terraform Platform Layer

Terraform owns cluster and security resources that must exist before ArgoCD syncs the application. ArgoCD owns application workloads only.

## Ownership Boundary

### Terraform owns (platform / security layer)

| Resource type | Module | Namespace |
|---|---|---|
| Namespaces (`codereview`, `vault`) | `namespace` | — |
| ServiceAccounts (sa-react-app, sa-dotnet-api, sa-php-service, sa-mysql, sa-grafana, prometheus) | `service-accounts` | `codereview` |
| Prometheus ClusterRole + ClusterRoleBinding | `rbac/prometheus` | cluster-scoped |
| Vault server — standalone, file storage PVC (Helm release) | `vault` | `vault` |
| Vault Secrets Operator (Helm release) | `vault-secrets-operator` | `vault` |
| VaultConnection CR | `vault-secrets-operator` | `codereview` |
| Vault Kubernetes auth backend + config | `vault-auth` | Vault |
| Vault auth roles (`dotnet-api-role`, `grafana-role`) | `vault-auth` | Vault |
| Vault policies (`codereview-dotnet-api`, `codereview-grafana`) | `vault-policies` | Vault |
| VaultAuth CRs (`dotnet-api-vault-auth`, `grafana-vault-auth`) | `vault-secrets-operator` | `codereview` |
| NetworkPolicies (12 policies, default-deny baseline) | `network-policies` | `codereview` |

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
| `terraform plan` | **No** | Requires live cluster — see below |
| `terraform apply` | **No** | Manual local operation only |

### Why plan/apply are not in CI

The kind environment uses two providers that require live endpoints at plan time:

- `hashicorp/kubernetes` — needs a reachable kubeconfig (kind cluster)
- `hashicorp/helm` — needs the same Kubernetes API

GitHub Actions runners have neither. Exposing the local kind cluster to CI is not viable. A remote environment with managed Kubernetes would enable plan/apply in CI, but that is out of scope for this kind-based setup.

**Consequence:** `terraform plan` must be run locally and reviewed before every `terraform apply`. This is a structural property of the kind environment, not a deferred TODO.

If a non-kind environment is introduced in the future, this document and the CI workflow should be updated to add plan (and optionally apply on a protected branch).

## Local kind Environment

### First apply (new cluster)

The Vault Terraform provider was removed because it validates its token against the Vault API
at plan time — before the Vault pod exists. Vault policies and auth roles are now configured
by `scripts/configure-vault.sh` (vault CLI) after Vault is initialized and unsealed.

```powershell
cd terraform/environments/kind
terraform init

# Stage A: install VSO Helm release — registers VaultConnection/VaultAuth CRDs
terraform apply "-target=module.vault_secrets_operator.helm_release.vso"

# Stage B: full apply — kubernetes_manifest resources can now plan against existing CRDs
terraform apply
```

### Subsequent applies (cluster already running)

```powershell
cd terraform/environments/kind
terraform plan   # review before applying
terraform apply
```

### Initialize and unseal Vault (once per cluster creation)

Run in **Git Bash** after `terraform apply`. Requires vault CLI in PATH.

```bash
export VAULT_ADDR=http://127.0.0.1:30200

# Sealed Vault never passes its readiness probe — use phase polling instead.
until kubectl get pod vault-0 -n vault -o jsonpath='{.status.phase}' 2>/dev/null | grep -q Running; do
  echo "Waiting for vault-0..."; sleep 5
done

vault operator init -key-shares=1 -key-threshold=1 | tee vault-init.txt
# ⚠ Save vault-init.txt to a password manager, then delete it.

UNSEAL_KEY=$(grep 'Unseal Key 1:' vault-init.txt | awk '{print $NF}')
export VAULT_TOKEN=$(grep 'Initial Root Token:' vault-init.txt | awk '{print $NF}')

vault operator unseal "$UNSEAL_KEY"
kubectl create secret generic vault-unseal-keys -n vault --from-literal=key="$UNSEAL_KEY"
rm vault-init.txt
```

**Auto-unseal behaviour:** The Vault pod has a `postStart` lifecycle hook that reads `/vault/userconfig/vault-unseal-keys/key` and calls `vault operator unseal`. On all subsequent pod restarts (crash recovery, rolling update, kind container stop/start), Vault unseals automatically.

**Data persistence:** Vault KV data is stored on a 1 Gi PVC. Data survives pod restarts and kind container stop/start. Data is lost only on `kind delete cluster` (which destroys the kind node and all PVCs). After a full cluster deletion, repeat init + configure + seed.

### Configure Vault (after init + unseal, once per cluster creation)

```bash
bash scripts/configure-vault.sh
```

Creates: KV v2 engine, ACL policies (codereview-dotnet-api, codereview-grafana, codereview-php-service), Kubernetes auth method, auth roles (dotnet-api-role, grafana-role).

### Seed Vault secrets (after configure-vault.sh, once per cluster creation)

With VAULT_ADDR and VAULT_TOKEN already exported from the init step:

```bash
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
| `modules/vault` | Vault server (standalone, file storage PVC) via Helm, NodePort 30200 |
| `modules/vault-secrets-operator` | VSO Helm release, VaultConnection CR, VaultAuth CRs |
| `modules/network-policies` | 12 NetworkPolicies: default-deny baseline + per-service allow rules |

Vault ACL policies and Kubernetes auth roles are **not** Terraform-managed. They are configured by `scripts/configure-vault.sh` (vault CLI) after Vault is initialized and unsealed. The `modules/vault-policies` and `modules/vault-auth` modules were removed because the Vault Terraform provider validates its token against the Vault API at plan time — before the Vault pod exists.

## Production Environment

`terraform/environments/prod` is a placeholder. No production target has been selected. When one is:

- Configure a remote backend with state locking before using Terraform for shared infrastructure.
- Add platform-specific provider configuration.
- Revisit the CI workflow — plan and apply in CI become viable once provider endpoints are stable and accessible.
- Do not commit production Terraform state or secrets to this directory.
