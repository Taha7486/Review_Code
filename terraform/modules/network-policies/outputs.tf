output "policy_names" {
  description = "Names of all NetworkPolicy resources created in the namespace."
  value = [
    kubernetes_network_policy.default_deny_all.metadata[0].name,
    kubernetes_network_policy.allow_dns_egress.metadata[0].name,
    kubernetes_network_policy.allow_react_app_ingress.metadata[0].name,
    kubernetes_network_policy.allow_react_app_egress.metadata[0].name,
    kubernetes_network_policy.allow_dotnet_api_ingress.metadata[0].name,
    kubernetes_network_policy.allow_dotnet_api_egress.metadata[0].name,
    kubernetes_network_policy.allow_mysql_ingress.metadata[0].name,
    kubernetes_network_policy.allow_php_service_ingress.metadata[0].name,
    kubernetes_network_policy.allow_prometheus_egress.metadata[0].name,
    kubernetes_network_policy.allow_prometheus_ingress.metadata[0].name,
    kubernetes_network_policy.allow_grafana_egress.metadata[0].name,
  ]
}
