FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json NuGet.config ./
COPY server/Directory.Build.props server/
COPY server/Directory.Packages.props server/
COPY server/database.json server/
COPY server/src/Identity/Devkit.Identity.Domain/Devkit.Identity.Domain.csproj server/src/Identity/Devkit.Identity.Domain/
COPY server/src/Identity/Devkit.Identity.Application/Devkit.Identity.Application.csproj server/src/Identity/Devkit.Identity.Application/
COPY server/src/Identity/Devkit.Identity.Infrastructure/Devkit.Identity.Infrastructure.csproj server/src/Identity/Devkit.Identity.Infrastructure/
COPY server/src/Identity/Devkit.Identity.Api/Devkit.Identity.Api.csproj server/src/Identity/Devkit.Identity.Api/
COPY server/src/Commons/Devkit.Commons/Devkit.Commons.csproj server/src/Commons/Devkit.Commons/
COPY server/src/Commons/Devkit.DomainCommons/Devkit.DomainCommons.csproj server/src/Commons/Devkit.DomainCommons/
COPY server/src/Commons/Devkit.Infrastructure/Devkit.Infrastructure.csproj server/src/Commons/Devkit.Infrastructure/
COPY server/src/Commons/Devkit.AspNetCore/Devkit.AspNetCore.csproj server/src/Commons/Devkit.AspNetCore/
RUN dotnet restore server/src/Identity/Devkit.Identity.Api/Devkit.Identity.Api.csproj --nologo
COPY server/src/Identity/ server/src/Identity/
COPY server/src/Commons/ server/src/Commons/
ARG VERSION=0.1.0
RUN dotnet publish server/src/Identity/Devkit.Identity.Api/Devkit.Identity.Api.csproj --configuration Release --no-restore --nologo --output /app/publish -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install --yes --no-install-recommends curl unzip && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=15s --retries=3 CMD curl --fail --silent http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Devkit.Identity.Api.dll"]
