# syntax=docker/dockerfile:1

# ============================================================================
# Dark Pro Blog — standalone Docker image.
# Everything the app needs (posts, media as bytes, comments) lives in one
# SQLite file; the only thing that needs to persist across container restarts
# is /app/data, mounted as a volume in docker-compose.yml.
# Structured JSON logs go to stdout (ELK via Docker logging driver / Filebeat)
# and optionally to /app/logs when that path is mounted.
# ============================================================================

# ---- Build stage ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BlogApp/BlogApp.csproj BlogApp/
RUN dotnet restore BlogApp/BlogApp.csproj

COPY BlogApp/. BlogApp/
WORKDIR /src/BlogApp
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 1654 appgroup \
    && useradd -u 1654 -g appgroup -m appuser \
    && mkdir -p /app/data /app/logs \
    && chown -R appuser:appgroup /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ForceHttps=false \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/blog.db" \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
VOLUME ["/app/data", "/app/logs"]

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "BlogApp.dll"]
