resource "kubernetes_cluster_role_v1" "prometheus" {
  metadata {
    name = "prometheus"
    labels = {
      "app.kubernetes.io/name"       = "prometheus"
      "app.kubernetes.io/component"  = "monitoring"
      "app.kubernetes.io/part-of"    = "codereview"
      "app.kubernetes.io/managed-by" = "terraform"
    }
  }

  rule {
    api_groups = [""]
    resources  = ["nodes", "nodes/proxy", "services", "endpoints", "pods"]
    verbs      = ["get", "list", "watch"]
  }

  rule {
    api_groups = ["networking.k8s.io", "extensions"]
    resources  = ["ingresses"]
    verbs      = ["get", "list", "watch"]
  }

  rule {
    non_resource_urls = ["/metrics"]
    verbs             = ["get"]
  }
}

resource "kubernetes_cluster_role_binding_v1" "prometheus" {
  metadata {
    name = "prometheus"
    labels = {
      "app.kubernetes.io/name"       = "prometheus"
      "app.kubernetes.io/component"  = "monitoring"
      "app.kubernetes.io/part-of"    = "codereview"
      "app.kubernetes.io/managed-by" = "terraform"
    }
  }

  role_ref {
    api_group = "rbac.authorization.k8s.io"
    kind      = "ClusterRole"
    name      = kubernetes_cluster_role_v1.prometheus.metadata[0].name
  }

  subject {
    kind      = "ServiceAccount"
    name      = var.service_account_name
    namespace = var.namespace
  }
}
