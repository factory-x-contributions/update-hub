#!/bin/bash

# SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
# SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
# SPDX-FileCopyrightText: 2026 Siemens AG
#
# SPDX-License-Identifier: Apache-2.0

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