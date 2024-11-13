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
        from_port       = var.container_port
        to_port         = var.container_port
        security_groups = [aws_security_group.lb.id]
    }

    egress {
        protocol    = "-1"
        from_port   = 0
        to_port     = 0
        cidr_blocks = ["0.0.0.0/0"]
    }
}

# ECS Task Definition with Container Definition
resource "aws_ecs_task_definition" "irs_container_task" {
  family                   = "irs-service-task"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"     # 0.25 vCPU
  memory                   = "512"     # 512 MiB
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn

  container_definitions = jsonencode([
    {
      name        = "irs-container"
      image       = var.image
      cpu         = 256
      memory      = 512
      essential   = true
      portMappings = [
        {
          containerPort = var.container_port
          hostPort      = var.container_port
          protocol      = "tcp"
        }
      ]
      environment = var.container_environment
      repositoryCredentials = {
        credentialsParameter = aws_secretsmanager_secret.github-pat-secret.arn
      },
      healthCheck = {
        retries = 10
        command = [ "CMD-SHELL", "curl -f http://localhost:8080/swagger/index.html || exit 1" ]
        timeout = 5
        interval = 10
        startPeriod = 10
      },
      logConfiguration = {
        logDriver = "awslogs",
        options = {
          awslogs-group = aws_cloudwatch_log_group.irs_log_group.name,
          awslogs-region = data.aws_region.current.name,
          awslogs-stream-prefix = "ecs"
        }
      },
    }
  ])
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
    container_name   = jsondecode(aws_ecs_task_definition.irs_container_task.container_definitions)[0].name
    container_port   = var.container_port
  }

  depends_on = [aws_alb_listener.irs, aws_iam_role_policy_attachment.ecs-task-execution-role-policy-attachment]
}
