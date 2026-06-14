# Vault Module

Installs HashiCorp Vault with the official Helm chart.

The local kind environment uses **standalone server mode** with file storage on a 1 Gi PVC, exposed through NodePort `30200`. Vault auto-unseals on pod restart via a `postStart` lifecycle hook that reads the unseal key from the `vault-unseal-keys` K8s Secret. Data persists across pod restarts and kind container stop/start; it is lost only on `kind delete cluster`.

Production must use HA storage, a proper auto-unseal backend (e.g. AWS KMS, GCP CKMS), and a remote Terraform state backend with locking.
