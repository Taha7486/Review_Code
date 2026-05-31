output "auth_path" {
  description = "Vault Kubernetes auth mount path."
  value       = vault_auth_backend.kubernetes.path
}

output "role_names" {
  description = "Vault Kubernetes auth role names."
  value       = keys(vault_kubernetes_auth_backend_role.this)
}
