output "cluster_role_name" {
  description = "Prometheus ClusterRole name."
  value       = kubernetes_cluster_role_v1.prometheus.metadata[0].name
}

output "cluster_role_binding_name" {
  description = "Prometheus ClusterRoleBinding name."
  value       = kubernetes_cluster_role_binding_v1.prometheus.metadata[0].name
}
