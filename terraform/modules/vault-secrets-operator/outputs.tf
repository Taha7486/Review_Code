output "vault_connection_name" {
  description = "VaultConnection name used by application VaultAuth resources."
  value       = var.vault_connection_name
}

output "vault_auth_names" {
  description = "VaultAuth names keyed by Terraform map key."
  value       = keys(var.vault_auths)
}
