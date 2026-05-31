# Vault Secrets Operator Module

Installs the Vault Secrets Operator with the official Helm chart.

The module also creates the shared `VaultConnection` and per-service `VaultAuth` resources in the application namespace. Application-owned `VaultStaticSecret` resources live under `k8s/base`.
