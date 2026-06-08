# SPDX-FileCopyrightText: 2026 Fraunhofer-Institut für Produktionstechnik und Automatisierung IPA
# SPDX-FileCopyrightText: 2026 Hilscher Gesellschaft für Systemautomation mbH
# SPDX-FileCopyrightText: 2026 Siemens AG
#
# SPDX-License-Identifier: Apache-2.0

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt-get update && apt-get install -y curl
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
# Used to pass the git hash to the build
ARG GIT_HASH=unknown
WORKDIR /src
COPY ["UpdateHub/", "UpdateHub/"]
RUN dotnet restore "UpdateHub/UpdateHub.csproj"
COPY . .
WORKDIR "/src/UpdateHub"
RUN ./updateGitHash.bash
RUN dotnet build "UpdateHub.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UpdateHub.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final

EXPOSE 8080

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UpdateHub.dll"]

HEALTHCHECK CMD curl --fail http://localhost:8080/healthz || exit

