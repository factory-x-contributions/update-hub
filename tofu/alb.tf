resource "aws_alb" "irs" {
  name            = "irs-load-balancer"
  subnets         = var.public_subnet_ids
  security_groups = [aws_security_group.lb.id]
}

resource "aws_alb_target_group" "irs" {
  name        = "irs-target-group"
  port        = var.container_port
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
      path    = "/swagger/index.html"
      port    = var.container_port
  }
}

# Redirect all traffic from the ALB to the target group
resource "aws_alb_listener" "irs" {
  load_balancer_arn = aws_alb.irs.id
  port              = var.container_port
  protocol          = "HTTP"

  default_action {
    target_group_arn = aws_alb_target_group.irs.id
    type             = "forward"
  }

  lifecycle {
    replace_triggered_by = [aws_alb_target_group.irs]
  }
}

# ALB security Group: Edit to restrict access to the application
resource "aws_security_group" "lb" {
    name        = "irs-load-balancer-security-group"
    description = "controls access to the ALB"
    vpc_id      = var.vpc_id

    ingress {
        protocol    = "tcp"
        from_port   = var.container_port
        to_port     = var.container_port
        cidr_blocks = ["0.0.0.0/0"]
    }

    egress {
        protocol    = "-1"
        from_port   = 0
        to_port     = 0
        cidr_blocks = ["0.0.0.0/0"]
    }
}
