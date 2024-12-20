resource "aws_cloudwatch_log_group" "irs_log_group" {
  name              = "/ecs/irs"
  retention_in_days = 30
}

resource "aws_cloudwatch_log_stream" "cb_log_stream" {
  name           = "irs-log-stream"
  log_group_name = aws_cloudwatch_log_group.irs_log_group.name
}