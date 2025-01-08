locals {
  config_source = "${path.module}/${var.config_file_path}"
}

module "configBucket" {
  source = "git::https://code.siemens.com/devops/iac/aws/tf-modules/s3-bucket"

  data_bucket_name              = "updateHub-databucket-${data.aws_caller_identity.current.account_id}"
  data_bucket_enable_versioning = true
  create_logging_bucket         = false

  tags = local.combined_tags
}

resource "aws_s3_object" "file_upload" {
  bucket      = module.configBucket.data_bucket_name
  key         = "config.yaml"
  source      = local.config_source
  source_hash = filemd5(local.config_source)
}