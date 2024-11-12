resource "aws_secretsmanager_secret" "github-pat-secret" {
  name        = "github-pat"
  description = "GitHub Personal Access Token for accessing private container registry"
}

resource "aws_secretsmanager_secret_version" "github-pat-secret-version" {
  secret_id     = aws_secretsmanager_secret.github-pat-secret.id
  secret_string = jsonencode({
    "username" = var.github_pat_username, 
    "password" = var.github_pat_token
  })
}

