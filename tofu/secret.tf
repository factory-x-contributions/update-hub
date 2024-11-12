resource "aws_secretsmanager_secret" "github_pat_secret" {
  name        = "github-pat"
  description = "GitHub Personal Access Token for accessing private container registry"
}

resource "aws_secretsmanager_secret_version" "github_pat_secret_version" {
  secret_id     = aws_secretsmanager_secret.github_pat_secret.id
  secret_string = jsonencode({
    { "username":"<gh-username>", "password":"<PAT-token>" }
  })
}

