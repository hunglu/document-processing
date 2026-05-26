output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "api_app_url" {
  description = "HTTPS URL of the API Web App"
  value       = "https://${azurerm_linux_web_app.api.default_hostname}"
}

output "worker_app_url" {
  description = "HTTPS URL of the Worker Web App"
  value       = "https://${azurerm_linux_web_app.worker.default_hostname}"
}

output "acr_login_server" {
  description = "Azure Container Registry login server URL"
  value       = azurerm_container_registry.main.login_server
}

output "cdn_endpoint_hostname" {
  description = "CDN endpoint hostname for page images"
  value       = azurerm_cdn_endpoint.pages.host_name
}

output "service_bus_namespace_name" {
  description = "Service Bus namespace name"
  value       = azurerm_servicebus_namespace.main.name
}

output "sql_server_fqdn" {
  description = "SQL Server fully qualified domain name"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "redis_hostname" {
  description = "Redis Cache hostname"
  value       = azurerm_redis_cache.main.hostname
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = azurerm_key_vault.main.vault_uri
}

output "storage_account_name" {
  description = "Storage account name"
  value       = azurerm_storage_account.main.name
}

output "api_managed_identity_principal_id" {
  description = "Principal ID of the API managed identity"
  value       = azurerm_linux_web_app.api.identity[0].principal_id
}

output "worker_managed_identity_principal_id" {
  description = "Principal ID of the Worker managed identity"
  value       = azurerm_linux_web_app.worker.identity[0].principal_id
}
