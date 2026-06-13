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


