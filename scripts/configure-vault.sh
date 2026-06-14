#!/usr/bin/env bash
# Configure Vault auth and policies for the CodeReview platform.
#
# This script replaces the vault_policies and vault_auth Terraform modules.
# Those modules used the Vault Terraform provider, which validates its token
# against the Vault API at plan time — before the Vault pod exists. Running
# this configuration via the vault CLI (after init + unseal) avoids that
# chicken-and-egg problem.
#
# Run AFTER vault operator init + unseal (step 3b in docs/cluster-setup.md).
# VAULT_ADDR and VAULT_TOKEN must be exported in the shell before calling.
set -euo pipefail

: "${VAULT_ADDR:=http://127.0.0.1:30200}"

if [ -z "${VAULT_TOKEN:-}" ]; then
  echo "ERROR: VAULT_TOKEN must be set to the root token from 'vault operator init'." >&2
  exit 1
fi

export VAULT_ADDR VAULT_TOKEN

if ! command -v vault >/dev/null 2>&1; then
  echo "ERROR: vault CLI is required but not found in PATH." >&2
  exit 1
fi

echo "Configuring Vault at ${VAULT_ADDR}..."

# ── KV v2 secret engine ───────────────────────────────────────────────────────
if ! vault secrets list -format=json | grep -q '"secret/"'; then
  vault secrets enable -path=secret kv-v2
  echo "  Enabled KV v2 at secret/"
fi

# ── ACL policies ──────────────────────────────────────────────────────────────
vault policy write codereview-dotnet-api - <<'EOF'
path "secret/data/codereview/dotnet-api/*" {
  capabilities = ["read"]
}
path "secret/metadata/codereview/dotnet-api/*" {
  capabilities = ["list", "read"]
}
path "secret/data/codereview/grafana/*" {
  capabilities = ["read"]
}
path "secret/metadata/codereview/grafana/*" {
  capabilities = ["list", "read"]
}
EOF
# NOTE: grafana paths are included here because VSO 0.9.x caches one Vault client
# per (VaultConnection + auth method + namespace). Both dotnet-api-vault-auth and
# grafana-vault-auth share this cache key, so VSO uses the dotnet-api token for
# all reads in the codereview namespace, including the grafana-secrets VaultStaticSecret.
echo "  Written policy: codereview-dotnet-api"

vault policy write codereview-grafana - <<'EOF'
path "secret/data/codereview/grafana/*" {
  capabilities = ["read"]
}
path "secret/metadata/codereview/grafana/*" {
  capabilities = ["list", "read"]
}
EOF
echo "  Written policy: codereview-grafana"

vault policy write codereview-php-service - <<'EOF'
path "secret/data/codereview/php-service/internal-bridge" {
  capabilities = ["read"]
}
path "secret/metadata/codereview/php-service/internal-bridge" {
  capabilities = ["read"]
}
EOF
echo "  Written policy: codereview-php-service"

# ── Kubernetes auth method ────────────────────────────────────────────────────
if ! vault auth list -format=json | grep -q '"kubernetes/"'; then
  vault auth enable kubernetes
  echo "  Enabled kubernetes auth method"
fi

vault write auth/kubernetes/config \
  kubernetes_host="https://kubernetes.default.svc"
echo "  Configured kubernetes auth backend"

# ── Auth roles ────────────────────────────────────────────────────────────────
vault write auth/kubernetes/role/dotnet-api-role \
  bound_service_account_names="sa-dotnet-api" \
  bound_service_account_namespaces="codereview" \
  policies="codereview-dotnet-api" \
  ttl="1h"
echo "  Created role: dotnet-api-role"

vault write auth/kubernetes/role/grafana-role \
  bound_service_account_names="sa-grafana" \
  bound_service_account_namespaces="codereview" \
  policies="codereview-grafana" \
  ttl="1h"
echo "  Created role: grafana-role"

echo ""
echo "Vault configuration complete. Run seed-vault.sh next to write secrets."
