output "github_pat_secret_arn" {
  value = aws_secretsmanager_secret.github-pat-secret.arn
}

output "irs_log_group_name" {
  value = aws_cloudwatch_log_group.irs_log_group.name
}