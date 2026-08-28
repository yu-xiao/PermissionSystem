FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/PermissionSystem.sln backend/
COPY backend/PermissionSystem.Api/PermissionSystem.Api.csproj backend/PermissionSystem.Api/
COPY backend/PermissionSystem.Application/PermissionSystem.Application.csproj backend/PermissionSystem.Application/
COPY backend/PermissionSystem.Domain/PermissionSystem.Domain.csproj backend/PermissionSystem.Domain/
COPY backend/PermissionSystem.Infrastructure/PermissionSystem.Infrastructure.csproj backend/PermissionSystem.Infrastructure/
COPY backend/PermissionSystem.McpServer/PermissionSystem.McpServer.csproj backend/PermissionSystem.McpServer/
COPY backend/PermissionSystem.Shared/PermissionSystem.Shared.csproj backend/PermissionSystem.Shared/
COPY backend/PermissionSystem.Worker/PermissionSystem.Worker.csproj backend/PermissionSystem.Worker/
COPY backend/PermissionSystem.Tests/PermissionSystem.Tests.csproj backend/PermissionSystem.Tests/
COPY backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj backend/PermissionSystem.UnitTests/
COPY backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj backend/PermissionSystem.IntegrationTests/
RUN dotnet restore backend/PermissionSystem.McpServer/PermissionSystem.McpServer.csproj
COPY backend/ backend/
RUN dotnet publish backend/PermissionSystem.McpServer/PermissionSystem.McpServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PermissionSystem.McpServer.dll"]
