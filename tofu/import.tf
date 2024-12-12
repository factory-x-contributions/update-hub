data "terraform_remote_state" "scaffold-infra" {
  backend = "s3"

  config = {
    bucket  = "state-bucket-${data.aws_caller_identity.current.account_id}"
    key     = "${var.repository}.tfstate"
  }
}