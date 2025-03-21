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