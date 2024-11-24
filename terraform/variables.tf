variable "common_name" {
  description = "The common name for the deployment"
  type        = string
}

variable "environment" {
  description = "The environment for the deployment"
  type        = string
}

variable "location" {
  description = "The location for the deployment"
  type        = string
}

variable "location_short" {
  description = "The short location for the deployment"
  type        = string
}

variable "azure_client_id" {
  description = "The Azure client ID"
  type        = string
}

variable "azure_client_secret" {
  description = "The Azure client secret"
  type        = string
  sensitive   = true
}

variable "azure_tenant_id" {
  description = "The Azure tenant ID"
  type        = string
}

variable "azure_subscription_id" {
  description = "The Azure subscription ID"
  type        = string
}

variable "enable_event_grid_webhook_subscription" {
  description = "Enable Event Grid Webhook Subscription"
  type        = bool
  default     = false
}

variable "webhook_receiver_allowed_app_ids" {
  description = "The application IDs (claim appId) allowed to access the webhook receiver"
  type        = list(string)
  default = [
    "4962773b-9cdb-44cf-a8bf-237846a00ab7", # Microsoft.EventGrid
  ]
}

variable "webhook_receiver_allowed_tenant_ids" {
  description = "The tenant IDs (claim tid) allowed to access the webhook receiver"
  type        = list(string)
}
