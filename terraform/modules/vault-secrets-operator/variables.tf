variable "namespace" {
  description = "Namespace where the Vault Secrets Operator is installed."
  type        = string
}

variable "app_namespace" {
  description = "Application namespace where VaultConnection and VaultAuth resources are created."
  type        = string
}

variable "release_name" {
  description = "Helm release name for the Vault Secrets Operator."
  type        = string
  default     = "vault-secrets-operator"
}

variable "chart_version" {
  description = "Vault Secrets Operator Helm chart version."
  type        = string
  default     = "0.9.1"
}

variable "vault_address" {
  description = "In-cluster Vault API address used by VSO."
  type        = string
}

variable "vault_connection_name" {
  description = "Name of the VaultConnection resource."
  type        = string
  default     = "vault-connection"
}

variable "vault_auths" {
  description = "VaultAuth resources keyed by name."
  type = map(object({
    role            = string
    service_account = string
  }))
  default = {}
}
