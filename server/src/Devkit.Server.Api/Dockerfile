FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json NuGet.config ./
COPY server/Directory.Build.props server/
COPY server/Directory.Packages.props server/
COPY server/src/Devkit.Server.Domain/Devkit.Server.Domain.csproj server/src/Devkit.Server.Domain/
COPY server/src/Devkit.Server.Application/Devkit.Server.Application.csproj server/src/Devkit.Server.Application/
COPY server/src/Devkit.Server.Infrastructure/Devkit.Server.Infrastructure.csproj server/src/Devkit.Server.Infrastructure/
COPY server/src/Devkit.Server.Api/Devkit.Server.Api.csproj server/src/Devkit.Server.Api/
RUN dotnet restore server/src/Devkit.Server.Api/Devkit.Server.Api.csproj --nologo

COPY server/ server/
ARG VERSION=0.1.0
RUN dotnet publish server/src/Devkit.Server.Api/Devkit.Server.Api.csproj \
    --configuration Release \
    --no-restore \
    --nologo \
    --output /app/publish \
    -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=15s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Devkit.Server.Api.dll"]
