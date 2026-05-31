# Terraform Platform Layer

Terraform owns cluster and security resources that must exist before ArgoCD syncs the application.

## Ownership Boundary

Terraform owns:

- namespaces
- ServiceAccounts
- RBAC
- future Vault platform resources
- future NetworkPolicies

ArgoCD owns:

- application Deployments and StatefulSets
- Services
- ConfigMaps that do not contain secrets
- image tag rollouts from the `deploy` branch

Do not add Terraform-owned resources to `k8s/base`, because ArgoCD syncs that path.

## Local kind Environment

```powershell
cd terraform/environments/kind
terraform init
terraform plan
terraform apply
```

The kind environment currently creates:

- `codereview` namespace
- `vault` namespace
- application ServiceAccounts in `codereview`
- Prometheus ClusterRole and ClusterRoleBinding
- Vault dev server in `vault`
- Vault Secrets Operator in `vault`
- Vault Kubernetes auth, service policies, and app `VaultAuth` resources

ArgoCD deploys application manifests into `codereview` but does not create that namespace.

## Vault Local Bootstrap

The local kind environment uses Vault dev mode with the root token `root`.

1. Ensure the kind cluster exposes NodePort `30200` from `kind-config.yaml`.
2. Apply the Vault server first:

```bash
cd terraform/environments/kind
terraform apply -target=module.vault
```

3. Apply Vault policies/auth and install the VSO Helm release:

```bash
terraform apply \
  -target=module.vault_policies \
  -target=module.vault_auth \
  -target=module.vault_secrets_operator.helm_release.vso
```

4. Apply the remaining resources, including `VaultConnection` and `VaultAuth` CRs:

```bash
terraform apply
```

This staged flow avoids a first-plan failure while VSO CRDs do not exist yet.

5. Seed placeholder values without storing them in Terraform state:

```bash
VAULT_ADDR=http://127.0.0.1:30200 VAULT_TOKEN=root ../../../scripts/seed-vault.sh
```

VSO then materializes the app-owned Kubernetes Secrets from `k8s/base/vault-static-secrets.yaml`:

- `dotnet-secrets` from `secret/codereview/dotnet-api/app`
- `mysql-secret` from `secret/codereview/dotnet-api/mysql`

Rotate the placeholder values before using this outside local development.

## Production Environment

`terraform/environments/prod` is intentionally a placeholder until the production platform is selected.

Configure a remote backend with locking before using Terraform for shared production infrastructure.
