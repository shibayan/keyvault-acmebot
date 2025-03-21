output "principal_id" {
  value       = azurerm_windows_function_app.function.identity[0].principal_id
  description = "Created Managed Identity Principal ID"
}

output "tenant_id" {
  value       = azurerm_windows_function_app.function.identity[0].tenant_id
  description = "Created Managed Identity Tenant ID"
}

output "allowed_ip_addresses" {
  value       = var.allowed_ip_addresses
  description = "IP addresses that are allowed to access the Acmebot UI."
}

# output "api_key" {
#   value       = data.azurerm_function_app_host_keys.function.default_function_key
#   description = "Created Default Functions API Key"
#   sensitive   = true
# }

output "func_app_name" {
  value       = azurerm_windows_function_app.function.name
  description = "Default Functions Hostname"
}

output "func_app_id" {
  value       = azurerm_windows_function_app.function.id
  description = "ID of  Functions App"
}

output "default_hostname" {
  value       = azurerm_windows_function_app.function.default_hostname
  description = "Default Functions Hostname"
}

output "custom_domain_verification_id" {
  value       = azurerm_windows_function_app.function.custom_domain_verification_id
  description = "Custom Domain Verification ID"
}