# syntax=docker/dockerfile:1
# Build context: the repository root (docker compose sets it; manually: docker build -f src/SmartTask.Api/Dockerfile .)

# ---- Stage 1: build and publish with the SDK ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore first, from the project files only, so this layer stays cached until a dependency changes.
COPY SmartTask.slnx Directory.Build.props ./
COPY src/SmartTask.Domain/SmartTask.Domain.csproj                 src/SmartTask.Domain/
COPY src/SmartTask.Application/SmartTask.Application.csproj       src/SmartTask.Application/
COPY src/SmartTask.Persistence/SmartTask.Persistence.csproj       src/SmartTask.Persistence/
COPY src/SmartTask.Infrastructure/SmartTask.Infrastructure.csproj src/SmartTask.Infrastructure/
COPY src/SmartTask.Api/SmartTask.Api.csproj                       src/SmartTask.Api/
RUN dotnet restore src/SmartTask.Api/SmartTask.Api.csproj

COPY src/ src/
RUN dotnet publish src/SmartTask.Api/SmartTask.Api.csproj \
    -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# ---- Stage 2: run on the ASP.NET Core runtime only ---------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Serilog's rolling file sink writes to /app/logs; the app runs as the image's non-root user.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "SmartTask.Api.dll"]
