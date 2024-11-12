resource "aws_iam_policy" "secrets_access_policy" {
  name        = "SecretsManagerAccessPolicy"
  description = "Allow ECS tasks to retrieve the GitHub PAT from Secrets Manager"
  policy = jsonencode({
    Version = "2012-10-17",
    Statement = [
      {
        Effect = "Allow",
        Action = [
          "secretsmanager:GetSecretValue"
        ],
        Resource = aws_secretsmanager_secret.github_pat_secret.arn
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "secrets_access_policy_attachment" {
  role       = aws_iam_role.ecs_task_execution_role.name
  policy_arn = aws_iam_policy.secrets_access_policy.arn
}