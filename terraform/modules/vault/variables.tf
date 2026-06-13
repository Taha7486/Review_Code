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

variable "storage_size" {
  description = "PVC size for Vault file storage. Keeps KV data across pod restarts."
  type        = string
  default     = "1Gi"
}

variable "ui_node_port" {
  description = "NodePort used to expose the Vault UI in the local kind environment."
  type        = number
  default     = 30200
}
