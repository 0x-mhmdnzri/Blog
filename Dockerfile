# syntax=docker/dockerfile:1.7
# =============================================================================
# AVICRM — .NET 10 multi-stage (fast cold start)
# SQLite: /app/data  |  Logs: /app/logs + stdout
# =============================================================================

ARG DOTNET_VERSION=10.0

# ---- restore (cached when csproj unchanged) ---------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-bookworm-slim AS restore
WORKDIR /src
COPY AVICRM/AVICRM.csproj AVICRM/
RUN dotnet restore AVICRM/AVICRM.csproj --verbosity quiet

# ---- publish ----------------------------------------------------------------
FROM restore AS build
COPY AVICRM/ AVICRM/
WORKDIR /src/AVICRM
# ReadyToRun = faster cold start; no single-file (simpler layer + volume mounts)
RUN dotnet publish AVICRM.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:PublishReadyToRun=true \
    -p:PublishReadyToRunComposite=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

# ---- runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-bookworm-slim AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appgroup \
    && useradd -r -g appgroup -d /app -s /sbin/nologin appuser \
    && mkdir -p /app/data /app/logs \
    && chown -R appuser:appgroup /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ForceHttps=false \
    RabbitMq__HostName=""

EXPOSE 8080
VOLUME ["/app/data", "/app/logs"]

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

HEALTHCHECK --interval=15s --timeout=3s --start-period=12s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "AVICRM.dll"]
