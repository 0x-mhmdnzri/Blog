# syntax=docker/dockerfile:1.7

# =============================================================================
# Dark Pro Blog — .NET 10 multi-stage image
# Data: SQLite at /app/data  |  Logs: /app/logs (JSON) + stdout for ELK
# =============================================================================

ARG DOTNET_VERSION=10.0

# ---- restore (layer-cached on csproj change) ---------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src
COPY BlogApp/BlogApp.csproj BlogApp/
RUN dotnet restore BlogApp/BlogApp.csproj --verbosity minimal

# ---- build + publish --------------------------------------------------------
FROM restore AS build
COPY BlogApp/ BlogApp/
WORKDIR /src/BlogApp
RUN dotnet publish BlogApp.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# curl: HEALTHCHECK only. tini optional alternative not needed — ASP.NET is PID1-safe enough.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 1654 appgroup \
    && useradd --uid 1654 --gid appgroup --create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /app/data /app/logs \
    && chown -R appuser:appgroup /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Asia/Tehran \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/blog.db" \
    ForceHttps=false

EXPOSE 8080

# Named volumes expected from compose; declared for documentation / docker run
VOLUME ["/app/data", "/app/logs"]

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/ || exit 1

ENTRYPOINT ["dotnet", "BlogApp.dll"]
