ARG DOTNET_VERSION=8.0
ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY Webhook/Webhook-Inventario.csproj Webhook/
RUN dotnet restore Webhook/Webhook-Inventario.csproj

FROM restore AS build
ARG BUILD_CONFIGURATION

COPY Webhook/ Webhook/
RUN dotnet build Webhook/Webhook-Inventario.csproj \
    -c ${BUILD_CONFIGURATION} \
    --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION

RUN dotnet publish Webhook/Webhook-Inventario.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-build \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD /bin/sh -c 'curl -fsS "http://localhost:${ASPNETCORE_HTTP_PORTS:-8080}/health" || exit 1'

ENTRYPOINT ["dotnet", "Webhook-Inventario.dll"]
