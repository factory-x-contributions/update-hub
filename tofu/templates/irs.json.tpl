[
  {
    "name": "${name}",
    "image": "${image}",
    "repositoryCredentials": {
      "credentialsParameter": "${repositoryCredentials}"
    },
    "cpu": 256,
    "memory": 512,
    "networkMode": "awsvpc",
    "logConfiguration": {
      "logDriver": "awslogs",
      "options": {
        "awslogs-group": "${log_group}",
        "awslogs-region": "${aws_region}",
        "awslogs-stream-prefix": "ecs"
      }
    },
    "portMappings": [
      {
        "containerPort": ${port}
      }
    ],
    "healthCheck": {
      "retries": 10,
      "command": [ "CMD-SHELL", "curl -f http://localhost:${port}/swagger/index.html || exit 1" ],
      "timeout": 5,
      "interval": 10,
      "startPeriod": 10
    }
  }
]