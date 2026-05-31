# Vault Module

Installs HashiCorp Vault with the official Helm chart.

The local kind environment uses dev mode and exposes the UI through NodePort `30200`. This is intentionally for local hardening work only; production must use HA storage and a real auto-unseal strategy.
