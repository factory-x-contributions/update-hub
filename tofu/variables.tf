variable "github_pat_username" {
  type        = string
}

variable "github_pat_token" {
  type        = string
  sensitive   = true
}


//---------------------------------------------------------------------

variable "image" {
  description = "Image URI"
  type = string
}

variable "container_port" {
  type = number
  default = 8080
}

variable "container_environment" {
  type = list(map(string))
  default = [{}]
}

//-----------------------------------------------------------------------

variable "vpc_id" {
  description = "The ID of the existing VPC."
  type        = string
}

variable "public_subnet_ids" {
  description = "The IDs of the existing subnets."
  type        = list(string)
}

variable "private_subnet_ids" {
  description = "The IDs of the existing subnets."
  type        = list(string)
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
