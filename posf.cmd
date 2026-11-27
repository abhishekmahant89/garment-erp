@echo off
setlocal enabledelayedexpansion

:: POSF - POS Fashion / GarmentERP CLI Launcher & Manager
set PROJECT_ROOT=%~dp0
cd /d "%PROJECT_ROOT%"

if "%~1"=="" goto menu
if /i "%~1"=="help" goto help
if /i "%~1"=="--help" goto help
if /i "%~1"=="-h" goto help
if /i "%~1"=="api" goto run_api
if /i "%~1"=="server" goto run_api
if /i "%~1"=="desktop" goto run_desktop
if /i "%~1"=="pos" goto run_desktop
if /i "%~1"=="all" goto run_all
if /i "%~1"=="test" goto run_test
if /i "%~1"=="build" goto run_build
if /i "%~1"=="publish" goto run_publish
if /i "%~1"=="docker" goto run_docker
if /i "%~1"=="docker-down" goto stop_docker
if /i "%~1"=="clean" goto run_clean
goto unknown

:menu
cls
echo ===============================================================================
echo                GARMENT STORE POS ^& RETAIL ERP - [ POSF ]
echo ===============================================================================
echo.
echo   [1] Start Web API Server       (ASP.NET Core 10 Web API + SignalR Hub)
echo   [2] Start Desktop POS Client   (WPF .NET 10 High-Speed Retail Terminal)
echo   [3] Start Both (API + POS)     (Launches Server in background ^& opens POS)
echo   [4] Run Automated Tests        (Executes all 22 Unit Tests)
echo   [5] Build Solution             (Release Configuration)
echo   [6] Publish Desktop App        (Self-contained Windows executable)
echo   [7] Start Docker Compose       (PostgreSQL 16 + Web API)
echo   [8] Stop Docker Compose        (Tear down containers)
echo   [9] View Sync ^& System Status  (Check local database ^& ports)
echo   [0] Exit
echo.
echo ===============================================================================
set /p opt="Select an option [0-9]: "

if "%opt%"=="1" goto run_api
if "%opt%"=="2" goto run_desktop
if "%opt%"=="3" goto run_all
if "%opt%"=="4" goto run_test
if "%opt%"=="5" goto run_build
if "%opt%"=="6" goto run_publish
if "%opt%"=="7" goto run_docker
if "%opt%"=="8" goto stop_docker
if "%opt%"=="9" goto run_status
if "%opt%"=="0" exit /b 0
goto menu

:run_api
echo.
echo [*] Starting GarmentERP Web API (http://localhost:5000)...
dotnet run --project src\GarmentERP.Api\GarmentERP.Api.csproj
goto end

:run_desktop
echo.
echo [*] Launching GarmentERP Desktop POS Terminal...
dotnet run --project src\GarmentERP.Desktop\GarmentERP.Desktop.csproj
goto end

:run_all
echo.
echo [*] Launching Web API in a separate window...
start "GarmentERP API Server" cmd /k "cd /d %PROJECT_ROOT% && dotnet run --project src\GarmentERP.Api\GarmentERP.Api.csproj"
echo [*] Waiting 3 seconds for API to initialize...
timeout /t 3 /nobreak >nul
echo [*] Launching Desktop POS...
start "GarmentERP Desktop POS" cmd /k "cd /d %PROJECT_ROOT% && dotnet run --project src\GarmentERP.Desktop\GarmentERP.Desktop.csproj"
goto end

:run_test
echo.
echo [*] Running all automated tests...
dotnet test GarmentERP.slnx --logger "console;verbosity=normal"
pause
goto end

:run_build
echo.
echo [*] Building GarmentERP solution in Release mode...
dotnet build GarmentERP.slnx -c Release
pause
goto end

:run_publish
echo.
echo [*] Publishing self-contained Desktop POS application for Windows x64...
dotnet publish src\GarmentERP.Desktop\GarmentERP.Desktop.csproj -c Release -r win-x64 --self-contained -o publish\desktop
echo.
echo [OK] Published to %PROJECT_ROOT%publish\desktop\GarmentERP.Desktop.exe
pause
goto end

:run_docker
echo.
echo [*] Starting Docker Compose (PostgreSQL 16 + Web API)...
docker compose up -d --build
echo.
echo [OK] Containers started. Run 'docker compose ps' to verify.
pause
goto end

:stop_docker
echo.
echo [*] Stopping Docker Compose containers...
docker compose down
echo.
echo [OK] Containers stopped.
pause
goto end

:run_status
echo.
echo ===============================================================================
echo                             SYSTEM ^& SYNC STATUS
echo ===============================================================================
echo.
echo [*] Checking local API port (5000):
netstat -ano | findstr :5000 >nul
if !errorlevel! equ 0 (
    echo     API Server: RUNNING on port 5000
) else (
    echo     API Server: NOT RUNNING
)
echo.
echo [*] Checking PostgreSQL port (5432):
netstat -ano | findstr :5432 >nul
if !errorlevel! equ 0 (
    echo     PostgreSQL: RUNNING on port 5432
) else (
    echo     PostgreSQL: NOT RUNNING (Local SQLite will be used for offline POS)
)
echo.
pause
goto end

:unknown
echo Unknown command: %1
echo.
:help
echo POSF - GarmentERP Command Line Tool
echo.
echo Usage:
echo   posf                  Opens interactive management menu
echo   posf api              Starts ASP.NET Core 10 Web API
echo   posf desktop          Starts WPF Desktop POS Terminal
echo   posf all              Launches both API and Desktop POS
echo   posf test             Runs all 22 unit tests
echo   posf build            Builds solution in Release configuration
echo   posf publish          Publishes standalone Desktop POS executable
echo   posf docker           Starts Docker Compose (PostgreSQL 16 + Web API)
echo   posf docker-down      Stops Docker Compose containers
echo   posf help             Shows this help screen
echo.
goto end

:end
