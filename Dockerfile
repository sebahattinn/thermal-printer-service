# syntax=docker/dockerfile:1.7

# ---------------------------------------------------------------------------
# Stage 1 - build: .NET 8 SDK uzerinde restore + publish
# ---------------------------------------------------------------------------
ARG DOTNET_SDK_TAG=8.0
ARG DOTNET_RUNTIME_TAG=8.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build
WORKDIR /src

# Once yalnizca proje dosyalarini kopyala ki "dotnet restore" katmani
# kaynak kod degisikliklerinde bosa cikmasin (Docker layer cache).
COPY ThermalPrinterService.sln ./
COPY src/ThermalPrinterService.Domain/ThermalPrinterService.Domain.csproj                 src/ThermalPrinterService.Domain/
COPY src/ThermalPrinterService.Application/ThermalPrinterService.Application.csproj       src/ThermalPrinterService.Application/
COPY src/ThermalPrinterService.Infrastructure/ThermalPrinterService.Infrastructure.csproj src/ThermalPrinterService.Infrastructure/
COPY src/ThermalPrinterService.Api/ThermalPrinterService.Api.csproj                       src/ThermalPrinterService.Api/
COPY tests/ThermalPrinterService.UnitTests/ThermalPrinterService.UnitTests.csproj         tests/ThermalPrinterService.UnitTests/
COPY tests/ThermalPrinterService.IntegrationTests/ThermalPrinterService.IntegrationTests.csproj tests/ThermalPrinterService.IntegrationTests/

RUN dotnet restore src/ThermalPrinterService.Api/ThermalPrinterService.Api.csproj

# Tum kaynagi kopyala ve publish et. UseAppHost=false: native launcher uretme,
# image kucuk kalsin; ENTRYPOINT zaten "dotnet *.dll" cagiriyor.
COPY . .
RUN dotnet publish src/ThermalPrinterService.Api/ThermalPrinterService.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false \
        /p:DebugType=none \
        /p:DebugSymbols=false

# ---------------------------------------------------------------------------
# Stage 2 - runtime: minimal ASP.NET Core runtime, root olmayan kullanici
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_TAG} AS runtime
WORKDIR /app

# Healthcheck icin curl; ek bagimliliklari tutmadan.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# .NET 8 aspnet image'i 'app' adli rootless user (uid 1654) ile gelir.
# Yeniden yaratmaya calismayiz; sadece yazici icin log dizinini hazirlayip
# sahipligini bu mevcut user'a veririz.
RUN mkdir -p /var/log/printer /app/failed-jobs \
 && chown -R app:app /var/log/printer /app/failed-jobs

COPY --from=build --chown=app:app /app/publish ./

USER app

EXPOSE 8080

# Container icindeki tum varsayilanlar; docker-compose veya `-e` ile override edilir.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_PRINT_TELEMETRY_MESSAGE=false \
    Printer__Logs__FilePath=/var/log/printer/logs.json \
    Printer__Logs__FailedJobsDirectory=/app/failed-jobs

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/status || exit 1

ENTRYPOINT ["dotnet", "ThermalPrinterService.Api.dll"]
