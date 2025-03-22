# We strongly recommend using the required_providers block to set the
# Azure Provider source and version being used
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">=4.22.0"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "5.1.0"
    }
  }
  backend "azurerm" {
    resource_group_name  = "rg-wus2-base-infra-01"                       # Can be passed via `-backend-config=`"resource_group_name=<resource group name>"` in the `init` command.
    storage_account_name = "stgwus2baseinfra01"                          # Can be passed via `-backend-config=`"storage_account_name=<storage account name>"` in the `init` command.
    container_name       = "terraform-workload-tfstate"                  # Can be passed via `-backend-config=`"container_name=<container name>"` in the `init` command.
    key                  = "WUS2-DEV-keyvault-acmebot.terraform.tfstate" # Can be passed via `-backend-config=`"key=<blob key name>"` in the `init` command.
    use_oidc             = true                                          # Can also be set via `ARM_USE_OIDC` environment variable.
    client_id            = "ceba5876-6c50-4a1c-962d-a9eca969dace"        # Can also be set via `ARM_CLIENT_ID` environment variable.
    subscription_id      = "1ac7d30e-1440-4f30-9544-3fb860f07736"        # Can also be set via `ARM_SUBSCRIPTION_ID` environment variable.
    tenant_id            = "d312c5af-612c-4812-b977-2b95c20f7182"        # Can also be set via `ARM_TENANT_ID` environment variable.
    use_azuread_auth     = true                                          # Can also be set via `ARM_USE_AZUREAD` environment variable.
  }
}

# Configure the Microsoft Azure Provider
provider "azurerm" {
  features {
    log_analytics_workspace {
      permanently_delete_on_destroy = true
    }

    key_vault {
      purge_soft_delete_on_destroy               = false
      recover_soft_deleted_key_vaults            = true
      purge_soft_deleted_certificates_on_destroy = false
      purge_soft_deleted_keys_on_destroy         = false
      purge_soft_deleted_secrets_on_destroy      = false
    }
  }
  subscription_id = "1ac7d30e-1440-4f30-9544-3fb860f07736"
}

provider "cloudflare" {
  api_token = "bSS_4vYGtw--wWCUdJlRjz7H3C4tGF4DicIE6KVk"
}