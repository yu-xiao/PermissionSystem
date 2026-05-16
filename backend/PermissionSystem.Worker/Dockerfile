FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/PermissionSystem.sln backend/
COPY backend/PermissionSystem.Api/PermissionSystem.Api.csproj backend/PermissionSystem.Api/
COPY backend/PermissionSystem.Application/PermissionSystem.Application.csproj backend/PermissionSystem.Application/
COPY backend/PermissionSystem.Domain/PermissionSystem.Domain.csproj backend/PermissionSystem.Domain/
COPY backend/PermissionSystem.Infrastructure/PermissionSystem.Infrastructure.csproj backend/PermissionSystem.Infrastructure/
COPY backend/PermissionSystem.Shared/PermissionSystem.Shared.csproj backend/PermissionSystem.Shared/
COPY backend/PermissionSystem.Worker/PermissionSystem.Worker.csproj backend/PermissionSystem.Worker/

RUN dotnet restore backend/PermissionSystem.sln

COPY backend/ backend/
RUN dotnet publish backend/PermissionSystem.Worker/PermissionSystem.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PermissionSystem.Worker.dll"]
