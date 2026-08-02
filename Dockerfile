# syntax=docker/dockerfile:1.7
# =============================================================================
# Dark Pro Blog — .NET 10 multi-stage (fast cold start)
# SQLite: /app/data  |  Logs: /app/logs + stdout
#
# .NET 10 default tags use Ubuntu 24.04 "Noble" (no bookworm-slim).
# Runtime stage avoids apt-get (flaky mirrors / exit 4) — no curl install.
# Base aspnet image already ships non-root user app (UID 1654 / $APP_UID).
# =============================================================================

ARG DOTNET_VERSION=10.0

# ---- restore (cached when csproj unchanged) ---------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS restore
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
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS runtime
WORKDIR /app

# Official image provides non-root "app" ($APP_UID=1654). No apt needed.
RUN mkdir -p /app/data /app/logs \
    && chown -R $APP_UID:$APP_UID /app/data /app/logs

# Image defaults only — Compose injects full config via env_file (.env).
# Never bake secrets here; override with .env / -e at runtime.
ENV ASPNETCORE_HTTP_PORTS=8934 \
    ASPNETCORE_URLS=http://+:8934 \
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

EXPOSE 8934
VOLUME ["/app/data", "/app/logs"]

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

USER $APP_UID

# No curl in image: open TCP to listen port (app must be up). Compose also
# probes http://127.0.0.1:8934/health from the host network when needed.
HEALTHCHECK --interval=15s --timeout=3s --start-period=15s --retries=5 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8934' || exit 1

ENTRYPOINT ["dotnet", "BlogApp.dll"]
