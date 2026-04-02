FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY WalletApi.sln .
COPY WalletApi.API/WalletApi.API.csproj                                 WalletApi.API/
COPY WalletApi.Application/WalletApi.Application.csproj                 WalletApi.Application/
COPY WalletApi.Domain/WalletApi.Domain.csproj                           WalletApi.Domain/
COPY WalletApi.Infrastructure/WalletApi.Infrastructure.csproj           WalletApi.Infrastructure/

RUN dotnet restore WalletApi.API/WalletApi.API.csproj

COPY . .
RUN dotnet publish WalletApi.API/WalletApi.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_launchSettingsProfile=false

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

RUN mkdir -p /app/logs && chown -R appuser:appgroup /app

COPY --from=build --chown=appuser:appgroup /app/publish .

RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*

USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "WalletApi.API.dll"]