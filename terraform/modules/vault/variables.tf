variable "namespace" {
  description = "Namespace where Vault is installed."
  type        = string
}

variable "release_name" {
  description = "Helm release name for Vault."
  type        = string
  default     = "vault"
}

variable "chart_version" {
  description = "HashiCorp Vault Helm chart version."
  type        = string
  default     = "0.28.1"
}

variable "dev_root_token" {
  description = "Root token used by the Vault dev server. Dev-only; do not use for production."
  type        = string
  default     = "root"
  sensitive   = true
}

variable "ui_node_port" {
  description = "NodePort used to expose the Vault UI in the local kind environment."
  type        = number
  default     = 30200
}
