#!/bin/bash
# Stable start script for UpdateHub

# Set environment variables for stable networking
export ASPNETCORE_URLS="http://0.0.0.0:5292"
export ASPNETCORE_ENVIRONMENT="Development"

# Navigate to project directory
cd "$(dirname "$0")"

# Start the application
echo "Starting UpdateHub with stable configuration..."
echo "Listening on: $ASPNETCORE_URLS"

dotnet run --project UpdateHub.csproj