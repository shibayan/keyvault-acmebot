data "azurerm_client_config" "current" {

}

data "azurerm_resource_group" "certbot" {
  name = "rg-wus2-cert-bot-01"
}

data "azurerm_log_analytics_workspace" "default" {
  name                = "log-wus2-base-infra-01"
  resource_group_name = "rg-wus2-base-infra-01"
}

resource "random_string" "random" {
  length  = 4
  lower   = true
  upper   = false
  special = false
}

resource "random_uuid" "user_impersonation" {}

resource "random_uuid" "app_role_issue" {}

resource "random_uuid" "app_role_revoke" {}

resource "time_rotating" "default" {
  rotation_days = 180
}

resource "azurerm_key_vault" "default" {
  name                = "kv-${var.app_base_name}"
  resource_group_name = data.azurerm_resource_group.certbot.name
  location            = data.azurerm_resource_group.certbot.location

  sku_name = "standard"

  enable_rbac_authorization = true
  tenant_id                 = data.azurerm_client_config.current.tenant_id
}

module "keyvault_acmebot" {
  source = ".//modules/keyvault_acmebot"

  app_base_name        = var.app_base_name
  resource_group_name  = data.azurerm_resource_group.certbot.name
  location             = data.azurerm_resource_group.certbot.location
  mail_address         = "anil.aluru@networkneil.dev"
  vault_uri            = azurerm_key_vault.default.vault_uri
  allowed_ip_addresses = var.home_ips

  log_analytics_workspace = data.azurerm_log_analytics_workspace.default.id


  additional_app_settings = {
    MICROSOFT_PROVIDER_AUTHENTICATION_SECRET = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.default.vault_uri}/secrets/func-nn-certbot/f80152df47424e70a5459f95d1913691)",
  }

  auth_settings = {
    enabled = true
    active_directory = {
      client_id                  = "4628b810-2735-44ff-a833-19b296a7958d"
      client_secret_setting_name = "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET"
      client_secret              = ""
      tenant_auth_endpoint       = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0/"
    }
  }

  cloudflare = {
    api_token = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.default.vault_uri}/secrets/CloudflareLetsEncryptFuncCertbot)"
  }

}

data "azurerm_key_vault_certificate" "example" {
  name         = "wildcard-networkneil-dev"
  key_vault_id = azurerm_key_vault.default.id
}

resource "azurerm_app_service_certificate" "example" {
  name                = "wildcard-networkneil-dev"
  resource_group_name = data.azurerm_resource_group.certbot.name
  location            = data.azurerm_resource_group.certbot.location
  key_vault_secret_id = data.azurerm_key_vault_certificate.example.secret_id
}

resource "azurerm_app_service_custom_hostname_binding" "example" {
  hostname            = "certbot.networkneil.dev"
  app_service_name    = module.keyvault_acmebot.func_app_name
  resource_group_name = data.azurerm_resource_group.certbot.name
  ssl_state           = "SniEnabled"
  # thumbprint          = data.azurerm_key_vault_certificate.example.thumbprint
  thumbprint = azurerm_app_service_certificate.example.thumbprint
}

resource "azurerm_role_assignment" "func_kv_1" {
  scope                = azurerm_key_vault.default.id
  role_definition_name = "Key Vault Certificates Officer"
  principal_id         = module.keyvault_acmebot.principal_id
}

resource "azurerm_role_assignment" "func_kv_2" {
  scope                = azurerm_key_vault.default.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.keyvault_acmebot.principal_id
}

# CF
resource "cloudflare_dns_record" "func_cname" {
  zone_id = var.zone_id
  type    = "CNAME"
  content = module.keyvault_acmebot.default_hostname
  name    = "certbot.networkneil.dev"
  proxied = false
  ttl     = 60
}

resource "cloudflare_dns_record" "func_txt" {
  zone_id = var.zone_id
  type    = "TXT"
  comment = "Domain verification record"
  content = module.keyvault_acmebot.custom_domain_verification_id
  name    = "asuid.certbot"
  proxied = false
  ttl     = 60
}