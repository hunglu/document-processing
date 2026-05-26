variable "resource_group_name" {
  description = "Name of the Azure Resource Group"
  type        = string
  default     = "rg-document-processing"
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "Southeast Asia"
}

variable "environment" {
  description = "Deployment environment (dev, staging, prod)"
  type        = string
  default     = "prod"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod."
  }
}

variable "project_prefix" {
  description = "Short prefix used in resource names"
  type        = string
  default     = "docproc"
}

variable "sql_admin_login" {
  description = "SQL Server administrator login name"
  type        = string
  default     = "sqladmin"
  sensitive   = true
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
}

variable "acr_sku" {
  description = "Azure Container Registry SKU"
  type        = string
  default     = "Standard"
}

variable "app_service_plan_sku" {
  description = "App Service Plan SKU"
  type        = string
  default     = "P2v3"
}

variable "redis_sku_name" {
  description = "Redis Cache SKU (Basic, Standard, Premium)"
  type        = string
  default     = "Standard"
}

variable "redis_family" {
  description = "Redis Cache family (C, P)"
  type        = string
  default     = "C"
}

variable "redis_capacity" {
  description = "Redis Cache capacity (0-6 for C family)"
  type        = number
  default     = 1
}

variable "sql_database_sku" {
  description = "SQL Database SKU name"
  type        = string
  default     = "GP_S_Gen5_2"
}

variable "blob_immutability_period_days" {
  description = "Immutability policy period in days for the docs container (7 years = 2555 days)"
  type        = number
  default     = 2555
}

variable "service_bus_message_retention_days" {
  description = "Service Bus message retention in days"
  type        = number
  default     = 7
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default = {
    Project     = "DocumentProcessing"
    ManagedBy   = "Terraform"
    CostCenter  = "Engineering"
  }
}
