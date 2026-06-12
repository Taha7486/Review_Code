variable "namespace" {
  description = "Application namespace in which to create NetworkPolicies."
  type        = string
}

variable "labels" {
  description = "Common labels applied to all NetworkPolicy resources."
  type        = map(string)
  default = {
    "app.kubernetes.io/managed-by" = "terraform"
    "app.kubernetes.io/part-of"    = "codereview"
    "app.kubernetes.io/component"  = "network-policy"
  }
}
