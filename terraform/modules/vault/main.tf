resource "helm_release" "vault" {
  name       = var.release_name
  namespace  = var.namespace
  repository = "https://helm.releases.hashicorp.com"
  chart      = "vault"
  version    = var.chart_version

  # Do not wait for pod readiness: an uninitialized Vault pod fails its readiness
  # probe (vault status exits 2 for "sealed") and would cause a 5-minute timeout.
  # The Helm release succeeds as soon as the chart resources are applied.
  # Vault is initialized and unsealed manually in step 3b of docs/cluster-setup.md.
  wait = false

  values = [
    yamlencode({
      server = {
        # Standalone server mode: persistent file storage on a PVC.
        # Dev mode is intentionally disabled — dev mode loses all KV data on
        # every pod restart, which breaks VSO-synced secrets mid-session.
        standalone = {
          enabled = true
          config  = <<-HCL
            ui = true

            listener "tcp" {
              tls_disable     = 1
              address         = "[::]:8200"
              cluster_address = "[::]:8201"
            }

            storage "file" {
              path = "/vault/data"
            }
          HCL
        }

        # 1 Gi PVC keeps KV data across pod restarts and kind container stop/start.
        dataStorage = {
          enabled    = true
          size       = var.storage_size
          accessMode = "ReadWriteOnce"
        }

        # Mount the vault-unseal-keys K8s Secret (optional — does not exist on
        # first boot; created manually after vault operator init).
        volumes = [
          {
            name = "vault-unseal-keys"
            secret = {
              secretName = "vault-unseal-keys"
              optional   = true
            }
          }
        ]
        volumeMounts = [
          {
            name      = "vault-unseal-keys"
            mountPath = "/vault/userconfig/vault-unseal-keys"
            readOnly  = true
          }
        ]

        # Auto-unseal on pod restart: reads the unseal key from the mounted
        # secret (written once by the operator in step 3b of docs/cluster-setup.md).
        # The || true ensures the hook never fails — on first boot the file
        # does not exist yet and the operator unseals manually.
        lifecycle = {
          postStart = {
            exec = {
              command = [
                "/bin/sh", "-c",
                "sleep 5; [ -f /vault/userconfig/vault-unseal-keys/key ] && vault operator unseal \"$(cat /vault/userconfig/vault-unseal-keys/key)\" || true"
              ]
            }
          }
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
