terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "4.11.0"
    }
  }
}

provider "azurerm" {
  features {}
  client_id       = var.azure_client_id
  client_secret   = var.azure_client_secret
  tenant_id       = var.azure_tenant_id
  subscription_id = var.azure_subscription_id
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${var.environment}-${var.location_short}-${var.common_name}"
  location = var.location
}

resource "azurerm_storage_account" "this" {
  name                     = "sa${var.environment}${var.location_short}${var.common_name}fnwhreceiver"
  resource_group_name      = azurerm_resource_group.this.name
  location                 = azurerm_resource_group.this.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "law-${var.environment}-${var.location_short}-${var.common_name}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_application_insights" "this" {
  name                = "appi-${var.environment}-${var.location_short}-${var.common_name}"
  workspace_id        = azurerm_log_analytics_workspace.this.id
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  application_type    = "web"
}

resource "azurerm_eventgrid_topic" "this" {
  name                = "eg-topic-${var.environment}-${var.location_short}-${var.common_name}-webhook-bridge"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  local_auth_enabled  = false
  input_schema        = "CloudEventSchemaV1_0"
}

resource "azurerm_role_assignment" "this" {
  scope                = azurerm_eventgrid_topic.this.id
  role_definition_name = "EventGrid Data Sender"
  principal_id         = azurerm_linux_function_app.eventgrid_publisher.identity[0].principal_id
}

resource "azurerm_eventgrid_event_subscription" "this" {
  for_each = {
    for s in ["this"] : s => s
    if var.enable_event_grid_webhook_subscription
  }

  name                  = "eg-sub-${var.environment}-${var.location_short}-${var.common_name}-fnwhreceiver"
  scope                 = azurerm_eventgrid_topic.this.id
  event_delivery_schema = "CloudEventSchemaV1_0"
  webhook_endpoint {
    url                               = "https://${azurerm_linux_function_app.webhook_receiver.default_hostname}/api/webhook"
    active_directory_app_id_or_uri    = var.azure_client_id
    active_directory_tenant_id        = var.azure_tenant_id
    max_events_per_batch              = 1
    preferred_batch_size_in_kilobytes = 64
  }
}
