output "names" {
  description = "ServiceAccount names keyed by logical name."
  value = {
    for name, service_account in kubernetes_service_account_v1.this :
    name => service_account.metadata[0].name
  }
}
