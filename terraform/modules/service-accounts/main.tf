resource "kubernetes_service_account_v1" "this" {
  for_each = var.service_accounts

  metadata {
    name      = each.key
    namespace = var.namespace
    labels = merge(
      {
        "app.kubernetes.io/managed-by" = "terraform"
        "app.kubernetes.io/part-of"    = "codereview"
        "app.kubernetes.io/component"  = each.value.component
      },
      each.value.labels
    )
  }

  automount_service_account_token = each.value.automount_service_account_token
}
