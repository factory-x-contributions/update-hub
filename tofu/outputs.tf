output "github_pat_secret_arn" {
  value = aws_secretsmanager_secret.github-pat-secret.arn
}

output "irs_log_group_name" {
  value = aws_cloudwatch_log_group.irs_log_group.name
}

output "ecs_task_execution_role_arn" {
  value = aws_iam_role.ecs_task_execution_role.arn
}

output "aws_ecs_service_name" {
  value = aws_ecs_service.irs.name
}

output "aws_ecs_cluster_name" {
  value = aws_ecs_cluster.irs.name
}