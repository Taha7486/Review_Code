#!/usr/bin/env bash
set -euo pipefail

: "${VAULT_ADDR:=http://127.0.0.1:30200}"
: "${VAULT_TOKEN:=root}"

export VAULT_ADDR
export VAULT_TOKEN

if ! command -v vault >/dev/null 2>&1; then
  echo "vault CLI is required but was not found in PATH." >&2
  exit 1
fi

echo "Using Vault at ${VAULT_ADDR}"

if ! vault secrets list -format=json | grep -q '"secret/"'; then
  vault secrets enable -path=secret kv-v2
fi

vault kv put secret/codereview/dotnet-api/app \
  JWT_SECRET_KEY="replace_with_a_32_plus_character_jwt_secret" \
  INTERNAL_SERVICE_SECRET="replace_with_shared_internal_service_secret" \
  GITHUB_PAT="replace_with_github_pat_or_empty_placeholder"

vault kv put secret/codereview/dotnet-api/mysql \
  MYSQL_ROOT_PASSWORD="replace_with_mysql_root_password" \
  MYSQL_DATABASE="code_review_tool"

vault kv put secret/codereview/php-service/internal-bridge \
  INTERNAL_SERVICE_SECRET="replace_with_shared_internal_service_secret"

vault kv put secret/codereview/grafana/config \
  admin-password="replace_with_secure_grafana_admin_password"

echo "Vault seed complete. Rotate placeholders before using this outside local dev."
