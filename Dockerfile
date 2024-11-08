FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["irs/", "irs/"]
RUN dotnet restore "irs/irs.csproj"
COPY . .
WORKDIR "/src/irs"
RUN dotnet build "irs.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "irs.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final

EXPOSE 8080

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "irs.dll"]
