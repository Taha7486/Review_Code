resource "helm_release" "vso" {
  name       = var.release_name
  namespace  = var.namespace
  repository = "https://helm.releases.hashicorp.com"
  chart      = "vault-secrets-operator"
  version    = var.chart_version

  # CRDs are registered when chart resources are applied, not when the controller
  # pod is ready. wait = false avoids a 5-minute timeout on image pull.
  wait = false
}

resource "kubernetes_manifest" "vault_connection" {
  manifest = {
    apiVersion = "secrets.hashicorp.com/v1beta1"
    kind       = "VaultConnection"
    metadata = {
      name      = var.vault_connection_name
      namespace = var.app_namespace
      labels = {
        "app.kubernetes.io/managed-by" = "terraform"
        "app.kubernetes.io/part-of"    = "codereview"
      }
    }
    spec = {
      address = var.vault_address
    }
  }

  depends_on = [helm_release.vso]
}

resource "kubernetes_manifest" "vault_auth" {
  for_each = var.vault_auths

  manifest = {
    apiVersion = "secrets.hashicorp.com/v1beta1"
    kind       = "VaultAuth"
    metadata = {
      name      = each.key
      namespace = var.app_namespace
      labels = {
        "app.kubernetes.io/managed-by" = "terraform"
        "app.kubernetes.io/part-of"    = "codereview"
      }
    }
    spec = {
      vaultConnectionRef = var.vault_connection_name
      method             = "kubernetes"
      mount              = "kubernetes"
      kubernetes = {
        role           = each.value.role
        serviceAccount = each.value.service_account
      }
    }
  }

  depends_on = [kubernetes_manifest.vault_connection]
}
