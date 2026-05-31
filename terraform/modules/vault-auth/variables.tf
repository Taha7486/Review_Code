variable "auth_path" {
  description = "Vault Kubernetes auth mount path."
  type        = string
  default     = "kubernetes"
}

variable "kubernetes_host" {
  description = "Kubernetes API address reachable from Vault."
  type        = string
  default     = "https://kubernetes.default.svc"
}

variable "kubernetes_ca_cert" {
  description = "PEM encoded Kubernetes CA certificate. Leave null to use Vault defaults."
  type        = string
  default     = null
  sensitive   = true
}

variable "token_reviewer_jwt" {
  description = "JWT Vault uses to review Kubernetes service account tokens. Leave null for dev-mode defaults."
  type        = string
  default     = null
  sensitive   = true
}

variable "roles" {
  description = "Vault Kubernetes auth roles keyed by role name."
  type = map(object({
    bound_service_account_names      = list(string)
    bound_service_account_namespaces = list(string)
    policies                         = list(string)
    token_ttl                        = optional(number, 3600)
  }))
}
