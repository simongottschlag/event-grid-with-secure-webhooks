output "webhook_receiver_name" {
  description = "The name of the webhook receiver function app"
  value       = azurerm_linux_function_app.webhook_receiver.name
}

output "eventgrid_publisher_name" {
  description = "The name of the eventgrid publisher function app"
  value       = azurerm_linux_function_app.eventgrid_publisher.name
}

output "allowed_tenant_ids" {
  description = "The allowed tenant IDs for the webhook receiver function app"
  value       = local.allowed_tenant_ids
}

output "allowed_application_ids" {
  description = "The allowed application IDs for the webhook receiver function app"
  value       = local.allowed_application_ids
}

output "event_grid_topic_endpoint_uri" {
  description = "The endpoint URI of the event grid topic"
  value       = azurerm_eventgrid_topic.this.endpoint
}
