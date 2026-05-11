# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY kd-802x-portal.csproj kd-802x-portal.slnx ./
RUN dotnet restore kd-802x-portal.csproj

COPY . .
RUN dotnet publish kd-802x-portal.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# HEALTHCHECK で /healthz を叩くために curl を導入 (aspnet:10.0 base には未同梱)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

RUN useradd --uid 10001 --user-group --no-create-home --shell /sbin/nologin appuser \
 && chown -R appuser:appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsSL http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "kd-802x-portal.dll"]
