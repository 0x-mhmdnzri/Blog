# syntax=docker/dockerfile:1.7
# =============================================================================
# Dark Pro Blog — .NET 10 multi-stage (fast cold start)
# SQLite: /app/data  |  Logs: /app/logs + stdout
# =============================================================================

ARG DOTNET_VERSION=10.0

# ---- restore (cached when csproj unchanged) ---------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-bookworm-slim AS restore
WORKDIR /src
COPY BlogApp/BlogApp.csproj BlogApp/
RUN dotnet restore BlogApp/BlogApp.csproj --verbosity quiet

# ---- publish ----------------------------------------------------------------
FROM restore AS build
COPY BlogApp/ BlogApp/
WORKDIR /src/BlogApp
# ReadyToRun = faster cold start; no single-file (simpler layer + volume mounts)
RUN dotnet publish BlogApp.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:PublishReadyToRun=true \
    -p:PublishReadyToRunComposite=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

# ---- runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-bookworm-slim AS runtime
WORKDIR /app

# curl only for HEALTHCHECK; keep image lean
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 1654 appgroup \
    && useradd --uid 1654 --gid appgroup --create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /app/data /app/logs \
    && chown -R appuser:appgroup /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_TieredCompilation=1 \
    DOTNET_TC_QuickJitForLoops=1 \
    DOTNET_ReadyToRun=1 \
    TZ=Asia/Tehran \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/blog.db;Cache=Shared;Pooling=True;Default Timeout=30" \
    ForceHttps=false \
    RabbitMq__HostName=""

EXPOSE 8080
VOLUME ["/app/data", "/app/logs"]

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

# Lightweight endpoint — does not run full page pipeline
HEALTHCHECK --interval=15s --timeout=3s --start-period=12s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "BlogApp.dll"]
