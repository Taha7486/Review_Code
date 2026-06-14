#!/usr/bin/env bash
# Seed Vault KV secrets for the CodeReview platform.
#
# Run AFTER vault operator init + unseal (step 3b in docs/cluster-setup.md).
# VAULT_ADDR and VAULT_TOKEN must be exported in the shell before calling this script.
#
# All secrets except GITHUB_PAT are generated randomly on each run.
# On a fresh cluster this is always correct — Vault has no prior data.
# GITHUB_PAT: pass via env var if you have one; the app works without it.
set -euo pipefail

: "${VAULT_ADDR:=http://127.0.0.1:30200}"

if [ -z "${VAULT_TOKEN:-}" ]; then
  echo "ERROR: VAULT_TOKEN must be set to the root token from 'vault operator init'." >&2
  echo "       Export it before running this script:" >&2
  echo "         export VAULT_TOKEN=<root-token>" >&2
  exit 1
fi

export VAULT_ADDR
export VAULT_TOKEN

if ! command -v vault >/dev/null 2>&1; then
  echo "ERROR: vault CLI is required but not found in PATH." >&2
  exit 1
fi

echo "Using Vault at ${VAULT_ADDR}"

# ── Generate secrets ──────────────────────────────────────────────────────────
# openssl rand -hex N produces 2N hex characters — no special characters,
# safe in MySQL connection strings, YAML env values, and Bearer headers.

JWT_SECRET_KEY=$(openssl rand -hex 32)           # 64-char hex key for JWT signing
INTERNAL_SERVICE_SECRET=$(openssl rand -hex 24)  # 48-char shared secret (dotnet-api <-> php-service)
MYSQL_ROOT_PASSWORD=$(openssl rand -hex 16)      # 32-char MySQL root password
GRAFANA_ADMIN_PASSWORD=$(openssl rand -hex 16)   # 32-char Grafana admin password

# Accept a real PAT from the environment; fall back to a placeholder.
# The app returns 401 on GitHub-dependent endpoints without a valid PAT,
# but all other functionality works fine.
GITHUB_PAT="${GITHUB_PAT:-ghp_placeholder_update_with_vault_kv_patch}"

printf '\n=== Generated secrets — save to a password manager before closing this terminal ===\n'
printf 'JWT_SECRET_KEY:           %s\n' "$JWT_SECRET_KEY"
printf 'INTERNAL_SERVICE_SECRET:  %s\n' "$INTERNAL_SERVICE_SECRET"
printf 'MYSQL_ROOT_PASSWORD:      %s\n' "$MYSQL_ROOT_PASSWORD"
printf 'GRAFANA_ADMIN_PASSWORD:   %s\n' "$GRAFANA_ADMIN_PASSWORD"
printf 'GITHUB_PAT:               %s\n' "$GITHUB_PAT"
printf '===================================================================================\n\n'

# ── Enable KV v2 if not already mounted ──────────────────────────────────────
if ! vault secrets list -format=json | grep -q '"secret/"'; then
  vault secrets enable -path=secret kv-v2
fi

# ── Write secrets ─────────────────────────────────────────────────────────────
vault kv put secret/codereview/dotnet-api/app \
  JWT_SECRET_KEY="$JWT_SECRET_KEY" \
  INTERNAL_SERVICE_SECRET="$INTERNAL_SERVICE_SECRET" \
  GITHUB_PAT="$GITHUB_PAT"

vault kv put secret/codereview/dotnet-api/mysql \
  MYSQL_ROOT_PASSWORD="$MYSQL_ROOT_PASSWORD" \
  MYSQL_DATABASE="code_review_tool"

vault kv put secret/codereview/php-service/internal-bridge \
  INTERNAL_SERVICE_SECRET="$INTERNAL_SERVICE_SECRET"

vault kv put secret/codereview/grafana/config \
  admin-password="$GRAFANA_ADMIN_PASSWORD"

echo "Vault seed complete."
echo ""
echo "To update the GitHub PAT once you have one:"
echo "  vault kv patch secret/codereview/dotnet-api/app GITHUB_PAT=<your-pat>"
