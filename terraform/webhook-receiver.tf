resource "azurerm_service_plan" "webhook_receiver" {
  name                = "asp-${var.environment}-${var.location_short}-${var.common_name}-whreceiver"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  os_type             = "Linux"
  sku_name            = "Y1"
}

locals {
  allowed_tenant_ids      = join(",", var.webhook_receiver_allowed_tenant_ids)
  allowed_application_ids = join(",", var.webhook_receiver_allowed_app_ids)
}

resource "azurerm_linux_function_app" "webhook_receiver" {
  name                = "fn-${var.environment}-${var.location_short}-${var.common_name}-whreceiver"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  storage_account_name       = azurerm_storage_account.this.name
  storage_account_access_key = azurerm_storage_account.this.primary_access_key
  service_plan_id            = azurerm_service_plan.webhook_receiver.id

  site_config {
    application_insights_connection_string = azurerm_application_insights.this.connection_string
    application_stack {
      use_dotnet_isolated_runtime = true
      dotnet_version              = "8.0"
    }
  }

  app_settings = {
    ALLOWED_TENANT_IDS      = local.allowed_tenant_ids
    ALLOWED_APPLICATION_IDS = local.allowed_application_ids
  }

  lifecycle {
    ignore_changes = [
      app_settings["WEBSITE_RUN_FROM_PACKAGE"],
      app_settings["WEBSITE_MOUNT_ENABLED"],
      tags["hidden-link: /app-insights-conn-string"],
      tags["hidden-link: /app-insights-instrumentation-key"],
      tags["hidden-link: /app-insights-resource-id"],
    ]
  }
}
