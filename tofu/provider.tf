terraform {
  required_version = ">= 1.6.0" // Use the minimum OpenTofu version required

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 4.0.0" // Use the appropriate provider version
    }
  }
}

provider "aws" {
  region = "eu-central-1"
  default_tags {
    tags = local.combined_tags
  }
}
