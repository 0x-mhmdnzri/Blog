# syntax=docker/dockerfile:1.7
# =============================================================================
# Dark Pro Blog — .NET 10 multi-stage (fast cold start)
# SQLite: /app/data  |  Logs: /app/logs + stdout
#
# .NET 10 default tags use Ubuntu 24.04 "Noble" (no bookworm-slim).
# Runtime stage avoids apt-get (flaky mirrors). Base image user: app ($APP_UID).
# Restore/publish use RID linux-x64 so ReadyToRun assets match (NETSDK1047).
# =============================================================================

ARG DOTNET_VERSION=10.0
ARG RID=linux-x64

# ---- restore (cached when csproj unchanged) ---------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS restore
ARG RID
WORKDIR /src
COPY BlogApp/BlogApp.csproj BlogApp/
RUN dotnet restore BlogApp/BlogApp.csproj -r ${RID} --verbosity quiet

# ---- publish ----------------------------------------------------------------
FROM restore AS build
ARG RID
COPY BlogApp/ BlogApp/
WORKDIR /src/BlogApp
# Framework-dependent + ReadyToRun for faster cold start on linux-x64
RUN dotnet publish BlogApp.csproj \
    -c Release \
    -r ${RID} \
    -o /app/publish \
    --no-restore \
    --self-contained false \
    -p:UseAppHost=false \
    -p:PublishReadyToRun=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

# ---- runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS runtime
WORKDIR /app

# Official image provides non-root "app" ($APP_UID=1654). No apt needed.
RUN mkdir -p /app/data /app/logs \
    && chown -R $APP_UID:$APP_UID /app/data /app/logs

# Image defaults only — Compose injects full config via env_file (.env).
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

# No curl in image: TCP check on listen port.
HEALTHCHECK --interval=15s --timeout=3s --start-period=15s --retries=5 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8934' || exit 1

ENTRYPOINT ["dotnet", "BlogApp.dll"]
