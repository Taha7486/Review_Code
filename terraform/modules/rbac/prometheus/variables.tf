variable "namespace" {
  description = "Namespace containing the Prometheus ServiceAccount."
  type        = string
}

variable "service_account_name" {
  description = "Prometheus ServiceAccount name."
  type        = string
}
