# syntax=docker/dockerfile:1.7
# =============================================================================
# Dark Pro Blog — .NET 10 multi-stage
# SQLite: /app/data  |  Logs: /app/logs + stdout
#
# .NET 10 tags: Ubuntu 24.04 noble (no bookworm-slim).
# Framework-dependent publish (no ReadyToRun — avoids NETSDK1094 runtime pack).
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
RUN dotnet publish BlogApp.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:PublishReadyToRun=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# ---- runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS runtime
WORKDIR /app

RUN mkdir -p /app/data /app/logs \
    && chown -R $APP_UID:$APP_UID /app/data /app/logs

ENV ASPNETCORE_HTTP_PORTS=8934 \
    ASPNETCORE_URLS=http://+:8934 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_TieredCompilation=1 \
    DOTNET_TC_QuickJitForLoops=1 \
    TZ=Asia/Tehran \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/blog.db;Cache=Shared;Pooling=True;Default Timeout=30" \
    ForceHttps=false \
    RabbitMq__HostName=""

EXPOSE 8934
VOLUME ["/app/data", "/app/logs"]

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

USER $APP_UID

HEALTHCHECK --interval=15s --timeout=3s --start-period=15s --retries=5 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8934' || exit 1

ENTRYPOINT ["dotnet", "BlogApp.dll"]
