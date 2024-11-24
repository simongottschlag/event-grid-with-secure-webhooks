resource "azurerm_service_plan" "eventgrid_publisher" {
  name                = "asp-${var.environment}-${var.location_short}-${var.common_name}-egpublisher"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  os_type             = "Linux"
  sku_name            = "Y1"
}

resource "azurerm_linux_function_app" "eventgrid_publisher" {
  name                = "fn-${var.environment}-${var.location_short}-${var.common_name}-egpublisher"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  storage_account_name       = azurerm_storage_account.this.name
  storage_account_access_key = azurerm_storage_account.this.primary_access_key
  service_plan_id            = azurerm_service_plan.eventgrid_publisher.id

  site_config {
    application_insights_connection_string = azurerm_application_insights.this.connection_string
    application_stack {
      use_dotnet_isolated_runtime = true
      dotnet_version              = "8.0"
    }
  }

  app_settings = {
    "EVENT_GRID_TOPIC__topicEndpointUri" = azurerm_eventgrid_topic.this.endpoint
  }

  identity {
    type = "SystemAssigned"
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
