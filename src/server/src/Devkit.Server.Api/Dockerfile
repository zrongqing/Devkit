FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json NuGet.config ./
COPY src/server/Directory.Build.props src/server/
COPY src/server/src/Devkit.Server.Domain/Devkit.Server.Domain.csproj src/server/src/Devkit.Server.Domain/
COPY src/server/src/Devkit.Server.Application/Devkit.Server.Application.csproj src/server/src/Devkit.Server.Application/
COPY src/server/src/Devkit.Server.Infrastructure/Devkit.Server.Infrastructure.csproj src/server/src/Devkit.Server.Infrastructure/
COPY src/server/src/Devkit.Server.Api/Devkit.Server.Api.csproj src/server/src/Devkit.Server.Api/
RUN dotnet restore src/server/src/Devkit.Server.Api/Devkit.Server.Api.csproj --nologo

COPY src/server/ src/server/
ARG VERSION=0.1.0
RUN dotnet publish src/server/src/Devkit.Server.Api/Devkit.Server.Api.csproj \
    --configuration Release \
    --no-restore \
    --nologo \
    --output /app/publish \
    -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Devkit.Server.Api.dll"]
