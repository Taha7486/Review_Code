variable "kubeconfig_path" {
  description = "Path to the kubeconfig used by Terraform."
  type        = string
  default     = "~/.kube/config"
}

variable "kube_context" {
  description = "Kubernetes context for the local kind cluster."
  type        = string
  default     = "kind-kind"
}

variable "vault_address" {
  description = "Vault API address. Used once Vault resources are enabled."
  type        = string
  default     = "http://127.0.0.1:30200"
}

variable "vault_token" {
  description = "Vault token. The default is for local dev-mode Vault only."
  type        = string
  default     = "root"
  sensitive   = true
}

variable "vault_kubernetes_host" {
  description = "Kubernetes API address reachable from the Vault pod."
  type        = string
  default     = "https://kubernetes.default.svc"
}

variable "vault_kubernetes_ca_cert" {
  description = "Optional PEM encoded Kubernetes CA certificate for Vault Kubernetes auth."
  type        = string
  default     = null
  sensitive   = true
}

variable "vault_token_reviewer_jwt" {
  description = "Optional token reviewer JWT for Vault Kubernetes auth."
  type        = string
  default     = null
  sensitive   = true
}
