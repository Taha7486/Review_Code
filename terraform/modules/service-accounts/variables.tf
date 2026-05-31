variable "namespace" {
  description = "Namespace where ServiceAccounts are created."
  type        = string
}

variable "service_accounts" {
  description = "ServiceAccounts keyed by name."
  type = map(object({
    component                       = string
    labels                          = optional(map(string), {})
    automount_service_account_token = optional(bool, true)
  }))
}
