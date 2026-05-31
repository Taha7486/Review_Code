output "service_name" {
  description = "Vault service name created by the Helm chart."
  value       = var.release_name
}

output "namespace" {
  description = "Vault namespace."
  value       = var.namespace
}

output "internal_address" {
  description = "In-cluster Vault API address."
  value       = "http://${var.release_name}.${var.namespace}.svc.cluster.local:8200"
}

output "ui_node_port" {
  description = "NodePort for the Vault UI."
  value       = var.ui_node_port
}
