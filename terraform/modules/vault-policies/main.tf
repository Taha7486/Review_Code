resource "vault_policy" "dotnet_api" {
  name = var.dotnet_policy_name

  policy = <<-EOT
    path "secret/data/codereview/dotnet-api/*" {
      capabilities = ["read"]
    }

    path "secret/metadata/codereview/dotnet-api/*" {
      capabilities = ["list", "read"]
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
