terraform {
  required_version = ">= 1.7"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.110"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-tfstate"
    storage_account_name = "stterraformstate"
    container_name       = "tfstate"
    key                  = "document-processing.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }
}

locals {
  prefix = "${var.project_prefix}-${var.environment}"
  tags   = merge(var.tags, { Environment = var.environment })
}

# ──────────────────────────────────────────────────────────────────────────────
# Resource Group
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

# ──────────────────────────────────────────────────────────────────────────────
# Key Vault
# ──────────────────────────────────────────────────────────────────────────────

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "main" {
  name                        = "kv-${local.prefix}"
  location                    = azurerm_resource_group.main.location
  resource_group_name         = azurerm_resource_group.main.name
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  sku_name                    = "standard"
  soft_delete_retention_days  = 90
  purge_protection_enabled    = true
  enable_rbac_authorization   = true
  tags                        = local.tags
}

# ──────────────────────────────────────────────────────────────────────────────
# Storage Account
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_storage_account" "main" {
  name                     = "st${replace(local.prefix, "-", "")}docs"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "ZRS"
  min_tls_version          = "TLS1_2"
  blob_properties {
    versioning_enabled = true
    delete_retention_policy {
      days = 30
    }
    container_delete_retention_policy {
      days = 7
    }
  }
  tags = local.tags
}

resource "azurerm_storage_container" "docs" {
  name                  = "docs"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "pages" {
  name                  = "pages"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

resource "azurerm_storage_management_policy" "lifecycle" {
  storage_account_id = azurerm_storage_account.main.id

  rule {
    name    = "tier-to-cool-after-30-days"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["docs/"]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_modification_greater_than    = 30
        tier_to_archive_after_days_since_modification_greater_than = 180
      }
    }
  }
}

# ──────────────────────────────────────────────────────────────────────────────
# Azure CDN
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_cdn_profile" "main" {
  name                = "cdn-${local.prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Standard_Microsoft"
  tags                = local.tags
}

resource "azurerm_cdn_endpoint" "pages" {
  name                = "cdn-${local.prefix}-pages"
  profile_name        = azurerm_cdn_profile.main.name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  origin {
    name      = "blob-origin"
    host_name = azurerm_storage_account.main.primary_blob_host
  }

  origin_host_header = azurerm_storage_account.main.primary_blob_host

  delivery_rule {
    name  = "EnforceHTTPS"
    order = 1
    request_scheme_condition {
      operator     = "Equal"
      match_values = ["HTTP"]
    }
    url_redirect_action {
      redirect_type = "Found"
      protocol      = "Https"
    }
  }

  tags = local.tags
}

# ──────────────────────────────────────────────────────────────────────────────
# Service Bus
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${local.prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Standard"
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "processing" {
  name                                    = "document-processing-queue"
  namespace_id                            = azurerm_servicebus_namespace.main.id
  max_delivery_count                      = 3
  default_message_ttl                     = "P7D"
  dead_lettering_on_message_expiration    = true
  lock_duration                           = "PT10M"
  enable_partitioning                     = false
}

resource "azurerm_servicebus_topic" "events" {
  name                         = "document-events"
  namespace_id                 = azurerm_servicebus_namespace.main.id
  default_message_time_to_live = "P7D"
}

resource "azurerm_servicebus_subscription" "events_all" {
  name                = "all-events"
  topic_id            = azurerm_servicebus_topic.events.id
  max_delivery_count  = 10
  default_message_ttl = "P7D"
}

# ──────────────────────────────────────────────────────────────────────────────
# Azure Cache for Redis
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_redis_cache" "main" {
  name                = "redis-${local.prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  capacity            = var.redis_capacity
  family              = var.redis_family
  sku_name            = var.redis_sku_name
  enable_non_ssl_port = false
  minimum_tls_version = "1.2"

  redis_configuration {
    maxmemory_policy = "allkeys-lru"
  }

  tags = local.tags
}

# ──────────────────────────────────────────────────────────────────────────────
# SQL Server + Database
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_mssql_server" "main" {
  name                         = "sql-${local.prefix}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"

  azuread_administrator {
    login_username = "AzureAD Admin"
    object_id      = data.azurerm_client_config.current.object_id
  }

  tags = local.tags
}

resource "azurerm_mssql_database" "main" {
  name                        = "db-document-processing"
  server_id                   = azurerm_mssql_server.main.id
  sku_name                    = var.sql_database_sku
  max_size_gb                 = 32
  zone_redundant              = false
  read_replica_count          = 0
  auto_pause_delay_in_minutes = 60

  short_term_retention_policy {
    retention_days           = 35
    backup_interval_in_hours = 12
  }

  long_term_retention_policy {
    weekly_retention  = "P4W"
    monthly_retention = "P12M"
    yearly_retention  = "P5Y"
    week_of_year      = 1
  }

  tags = local.tags
}

resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# ──────────────────────────────────────────────────────────────────────────────
# Azure Container Registry
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_container_registry" "main" {
  name                = "acr${replace(local.prefix, "-", "")}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = var.acr_sku
  admin_enabled       = false
  tags                = local.tags
}

# ──────────────────────────────────────────────────────────────────────────────
# App Service Plan + Web Apps
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_service_plan" "main" {
  name                = "asp-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_plan_sku
  tags                = local.tags
}

resource "azurerm_linux_web_app" "api" {
  name                = "app-${local.prefix}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
    always_on    = true
    http2_enabled = true
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"             = var.environment == "prod" ? "Production" : "Staging"
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    "DataDog__OtlpEndpoint"             = "http://localhost:4317"
  }

  tags = local.tags
}

resource "azurerm_linux_web_app" "worker" {
  name                = "app-${local.prefix}-worker"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
    always_on = true
  }

  app_settings = {
    "DOTNET_ENVIRONMENT" = var.environment == "prod" ? "Production" : "Staging"
    "DataDog__OtlpEndpoint" = "http://localhost:4317"
  }

  tags = local.tags
}

# ──────────────────────────────────────────────────────────────────────────────
# RBAC Role Assignments (managed identity → Azure services)
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_role_assignment" "api_blob_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

resource "azurerm_role_assignment" "worker_blob_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.worker.identity[0].principal_id
}

resource "azurerm_role_assignment" "api_sb_owner" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

resource "azurerm_role_assignment" "worker_sb_owner" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_linux_web_app.worker.identity[0].principal_id
}

resource "azurerm_role_assignment" "api_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

resource "azurerm_role_assignment" "worker_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.worker.identity[0].principal_id
}

resource "azurerm_role_assignment" "api_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

resource "azurerm_role_assignment" "worker_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.worker.identity[0].principal_id
}

# ──────────────────────────────────────────────────────────────────────────────
# Key Vault Secrets (store connection strings)
# ──────────────────────────────────────────────────────────────────────────────

resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "SqlConnectionString"
  value        = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Authentication=Active Directory Managed Identity;TrustServerCertificate=False;Encrypt=True;"
  key_vault_id = azurerm_key_vault.main.id
}

resource "azurerm_key_vault_secret" "redis_connection_string" {
  name         = "RedisConnectionString"
  value        = "${azurerm_redis_cache.main.hostname}:${azurerm_redis_cache.main.ssl_port},password=${azurerm_redis_cache.main.primary_access_key},ssl=True,abortConnect=False"
  key_vault_id = azurerm_key_vault.main.id
}

resource "azurerm_key_vault_secret" "service_bus_connection_string" {
  name         = "ServiceBusConnectionString"
  value        = azurerm_servicebus_namespace.main.default_primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
}
