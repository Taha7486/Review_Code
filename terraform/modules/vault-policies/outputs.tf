output "dotnet_policy_name" {
  description = "Vault policy name for the .NET API."
  value       = vault_policy.dotnet_api.name
}

output "php_policy_name" {
  description = "Vault policy name for the PHP analyzer service."
  value       = vault_policy.php_service.name
}

output "grafana_policy_name" {
  description = "Vault policy name for Grafana."
  value       = vault_policy.grafana.name
}
