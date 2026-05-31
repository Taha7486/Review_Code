output "codereview_namespace" {
  description = "Application namespace managed by Terraform."
  value       = module.namespaces["codereview"].name
}

output "vault_namespace" {
  description = "Vault namespace managed by Terraform."
  value       = module.namespaces["vault"].name
}

output "service_accounts" {
  description = "ServiceAccounts managed by Terraform in the codereview namespace."
  value       = module.service_accounts.names
}

output "vault_internal_address" {
  description = "In-cluster Vault address used by VSO."
  value       = module.vault.internal_address
}

output "vault_ui_node_port" {
  description = "Vault UI NodePort."
  value       = module.vault.ui_node_port
}
