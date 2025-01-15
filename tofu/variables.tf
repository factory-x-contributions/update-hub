variable "config_file_path" {
  type = string
  default = "./config.yaml"
}

//-----------------------------------------------------------------------

variable "repository" {
  description = "The GitHub repository name"
  type        = string
}

variable "default_tags" {
  description = "Default tags for all resources"
  type        = map(string)
  default = {
    environment = "dev"
    owner       = "Factory-X"
  }
}

locals {
  combined_tags = merge(var.default_tags, { repository = var.repository })
}
