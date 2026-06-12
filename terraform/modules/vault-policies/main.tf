resource "vault_policy" "dotnet_api" {
  name = var.dotnet_policy_name

  # Also covers grafana/config because vault-static-secrets.yaml uses dotnet-api-vault-auth
  # for all three VaultStaticSecrets. TODO: give grafana its own VaultAuth binding.
  policy = <<-EOT
    path "secret/data/codereview/dotnet-api/*" {
      capabilities = ["read"]
    }

    path "secret/metadata/codereview/dotnet-api/*" {
      capabilities = ["list", "read"]
    }

    path "secret/data/codereview/grafana/config" {
      capabilities = ["read"]
    }

    path "secret/metadata/codereview/grafana/config" {
      capabilities = ["read"]
    }
  EOT
}

resource "vault_policy" "php_service" {
  name = var.php_policy_name

  policy = <<-EOT
    path "secret/data/codereview/php-service/internal-bridge" {
      capabilities = ["read"]
    }

    path "secret/metadata/codereview/php-service/internal-bridge" {
      capabilities = ["read"]
    }
  EOT
}
