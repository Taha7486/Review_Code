variable "dotnet_policy_name" {
  description = "Vault policy name for the .NET API."
  type        = string
  default     = "codereview-dotnet-api"
}

variable "php_policy_name" {
  description = "Vault policy name for the PHP analyzer service."
  type        = string
  default     = "codereview-php-service"
}

variable "grafana_policy_name" {
  description = "Vault policy name for Grafana."
  type        = string
  default     = "codereview-grafana"
}
