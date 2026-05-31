resource "helm_release" "vault" {
  name       = var.release_name
  namespace  = var.namespace
  repository = "https://helm.releases.hashicorp.com"
  chart      = "vault"
  version    = var.chart_version

  values = [
    yamlencode({
      server = {
        dev = {
          enabled      = true
          devRootToken = var.dev_root_token
        }
      }
      ui = {
        enabled         = true
        serviceType     = "NodePort"
        serviceNodePort = var.ui_node_port
        externalPort    = 8200
        targetPort      = 8200
      }
    })
  ]
}
