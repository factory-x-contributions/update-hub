resource "aws_ecs_cluster" "irs" {
  name = "irs-cluster"
}

# Traffic to the ECS cluster should only come from the ALB
resource "aws_security_group" "ecs_tasks" {
    name        = "irs-ecs-tasks-security-group"
    description = "allow inbound access from the ALB only"
    vpc_id      = var.vpc_id

    ingress {
        protocol        = "tcp"
        from_port       = var.port
        to_port         = var.port
        security_groups = [aws_security_group.lb.id]
    }

    egress {
        protocol    = "-1"
        from_port   = 0
        to_port     = 0
        cidr_blocks = ["0.0.0.0/0"]
    }
}

data "template_file" "irs_container_definition" {
  template = file("./templates/irs.json.tpl")

  vars = {
    name                  = var.name
    image                 = var.image
    port                  = var.port
    repositoryCredentials = aws_secretsmanager_secret.github-pat-secret.arn
    aws_region            = data.aws_region.current.name
    log_group             = aws_cloudwatch_log_group.irs_log_group.name
  }
}

# ECS Task Definition with Container Definition
resource "aws_ecs_task_definition" "irs_container_task" {
  family                   = var.service_name
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"     # 0.25 vCPU
  memory                   = "512"     # 512 MiB
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn

  container_definitions    = data.template_file.irs_container_definition.rendered
}

resource "aws_ecs_service" "irs" {
  name            = "irs-service"
  cluster         = aws_ecs_cluster.irs.id
  task_definition = aws_ecs_task_definition.irs_container_task.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    security_groups  = [aws_security_group.ecs_tasks.id]
    subnets          = var.private_subnet_ids
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_alb_target_group.irs.id
    container_name   = var.name
    container_port   = var.port
  }

  lifecycle {
    ignore_changes = [task_definition]
  }

  depends_on = [aws_alb_listener.irs, aws_iam_role_policy_attachment.ecs-task-execution-role-policy-attachment]
}
