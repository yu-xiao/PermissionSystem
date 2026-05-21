@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

echo ========================================
echo PermissionSystem Backend Startup
echo ========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet was not found.
    echo Please install .NET 10 SDK first.
    echo.
    pause
    exit /b 1
)

echo [1/4] Checking .NET SDK...
dotnet --version
if errorlevel 1 goto failed
echo.

echo [2/4] Restoring NuGet packages...
dotnet restore "PermissionSystem.sln"
if errorlevel 1 goto failed
echo.

echo [3/4] Building backend solution...
dotnet build "PermissionSystem.sln" --no-restore
if errorlevel 1 goto failed
echo.

netstat -ano | findstr /R /C:":5264 .*LISTENING" >nul
if not errorlevel 1 (
    echo [ERROR] Port 5264 is already in use.
    echo Another backend process may already be running.
    echo.
    echo You can open: http://localhost:5264/swagger/index.html
    echo Or stop the existing process before running this script again.
    echo.
    pause
    exit /b 1
)

echo [4/4] Starting PermissionSystem.Api...
echo.
echo API:     http://localhost:5264
echo Swagger: http://localhost:5264/swagger/index.html
echo Login:   admin / configured SeedData:AdminPassword
echo.
echo Press Ctrl+C to stop the backend.
echo.

dotnet run --project "PermissionSystem.Api\PermissionSystem.Api.csproj" --launch-profile http --no-build
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo Backend process exited with code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%

:failed
echo.
echo [ERROR] Backend startup failed. Please check the error messages above.
pause
exit /b 1
