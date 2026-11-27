<#
.SYNOPSIS
    POSF - Garment & Clothing Store POS / Retail ERP Manager

.DESCRIPTION
    Command line tool to build, run, test, and deploy GarmentERP.

.EXAMPLE
    .\posf.ps1
    .\posf.ps1 api
    .\posf.ps1 desktop
    .\posf.ps1 all
    .\posf.ps1 test
    .\posf.ps1 build
    .\posf.ps1 docker
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("menu", "api", "server", "desktop", "pos", "all", "test", "build", "publish", "docker", "docker-down", "status", "help")]
    [string]$Command = "menu"
)

$ProjectRoot = $PSScriptRoot
Set-Location $ProjectRoot

function Show-Banner {
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host "               GARMENT STORE POS & RETAIL ERP - [ POSF ]" -ForegroundColor Yellow
    Write-Host "===============================================================================" -ForegroundColor Cyan
}

switch ($Command) {
    "menu" {
        Clear-Host
        Show-Banner
        Write-Host ""
        Write-Host "  [1] Start Web API Server       (ASP.NET Core 10 Web API + SignalR Hub)" -ForegroundColor Green
        Write-Host "  [2] Start Desktop POS Client   (WPF .NET 10 High-Speed Retail Terminal)" -ForegroundColor Green
        Write-Host "  [3] Start Both (API + POS)     (Launches Server in background & opens POS)" -ForegroundColor Yellow
        Write-Host "  [4] Run Automated Tests        (Executes all 22 Unit Tests)" -ForegroundColor Cyan
        Write-Host "  [5] Build Solution             (Release Configuration)" -ForegroundColor White
        Write-Host "  [6] Publish Desktop App        (Self-contained Windows executable)" -ForegroundColor Magenta
        Write-Host "  [7] Start Docker Compose       (PostgreSQL 16 + Web API)" -ForegroundColor Blue
        Write-Host "  [8] Stop Docker Compose        (Tear down containers)" -ForegroundColor DarkGray
        Write-Host "  [9] View System Status         (Check ports & local DB)" -ForegroundColor White
        Write-Host "  [0] Exit" -ForegroundColor Red
        Write-Host ""
        Write-Host "===============================================================================" -ForegroundColor Cyan
        $choice = Read-Host "Select an option [0-9]"
        switch ($choice) {
            "1" { & $MyInvocation.MyCommand.Path "api" }
            "2" { & $MyInvocation.MyCommand.Path "desktop" }
            "3" { & $MyInvocation.MyCommand.Path "all" }
            "4" { & $MyInvocation.MyCommand.Path "test" }
            "5" { & $MyInvocation.MyCommand.Path "build" }
            "6" { & $MyInvocation.MyCommand.Path "publish" }
            "7" { & $MyInvocation.MyCommand.Path "docker" }
            "8" { & $MyInvocation.MyCommand.Path "docker-down" }
            "9" { & $MyInvocation.MyCommand.Path "status" }
            "0" { exit }
            default { & $MyInvocation.MyCommand.Path "menu" }
        }
    }
    { $_ -in @("api", "server") } {
        Write-Host "`n[*] Starting GarmentERP Web API (http://localhost:5000)..." -ForegroundColor Green
        dotnet run --project src/GarmentERP.Api/GarmentERP.Api.csproj
    }
    { $_ -in @("desktop", "pos") } {
        Write-Host "`n[*] Launching GarmentERP Desktop POS Terminal..." -ForegroundColor Green
        dotnet run --project src/GarmentERP.Desktop/GarmentERP.Desktop.csproj
    }
    "all" {
        Write-Host "`n[*] Launching Web API in a separate window..." -ForegroundColor Cyan
        Start-Process pwsh -ArgumentList "-NoExit", "-Command", "Set-Location '$ProjectRoot'; dotnet run --project src/GarmentERP.Api/GarmentERP.Api.csproj"
        Start-Sleep -Seconds 3
        Write-Host "[*] Launching Desktop POS..." -ForegroundColor Green
        Start-Process pwsh -ArgumentList "-NoExit", "-Command", "Set-Location '$ProjectRoot'; dotnet run --project src/GarmentERP.Desktop/GarmentERP.Desktop.csproj"
    }
    "test" {
        Write-Host "`n[*] Running all automated unit tests..." -ForegroundColor Cyan
        dotnet test GarmentERP.slnx --logger "console;verbosity=normal"
    }
    "build" {
        Write-Host "`n[*] Building GarmentERP solution in Release mode..." -ForegroundColor Yellow
        dotnet build GarmentERP.slnx -c Release
    }
    "publish" {
        Write-Host "`n[*] Publishing self-contained Desktop POS application for Windows x64..." -ForegroundColor Magenta
        dotnet publish src/GarmentERP.Desktop/GarmentERP.Desktop.csproj -c Release -r win-x64 --self-contained -o publish/desktop
        Write-Host "`n[OK] Executable published to: $ProjectRoot\publish\desktop\GarmentERP.Desktop.exe" -ForegroundColor Green
    }
    "docker" {
        Write-Host "`n[*] Starting Docker Compose (PostgreSQL 16 + Web API)..." -ForegroundColor Blue
        docker compose up -d --build
        docker compose ps
    }
    "docker-down" {
        Write-Host "`n[*] Stopping Docker Compose..." -ForegroundColor DarkGray
        docker compose down
    }
    "status" {
        Show-Banner
        $apiPort = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
        if ($apiPort) {
            Write-Host "  API Server:  RUNNING on port 5000 (PID: $($apiPort.OwningProcess))" -ForegroundColor Green
        } else {
            Write-Host "  API Server:  NOT RUNNING (Run: posf api)" -ForegroundColor Red
        }

        $pgPort = Get-NetTCPConnection -LocalPort 5432 -ErrorAction SilentlyContinue
        if ($pgPort) {
            Write-Host "  PostgreSQL:  RUNNING on port 5432" -ForegroundColor Green
        } else {
            Write-Host "  PostgreSQL:  NOT RUNNING (Offline SQLite will be used by POS)" -ForegroundColor Yellow
        }
    }
    "help" {
        Write-Host "`nPOSF - GarmentERP Command Line Tool" -ForegroundColor Yellow
        Write-Host "`nUsage:"
        Write-Host "  posf                  Interactive menu"
        Write-Host "  posf api              Starts ASP.NET Core 10 Web API"
        Write-Host "  posf desktop          Starts WPF Desktop POS Terminal"
        Write-Host "  posf all              Starts both Server & Terminal"
        Write-Host "  posf test             Runs all 22 Unit Tests"
        Write-Host "  posf build            Builds solution in Release"
        Write-Host "  posf publish          Publishes standalone Desktop executable"
        Write-Host "  posf docker           Runs Docker Compose"
        Write-Host "  posf docker-down      Stops Docker containers"
        Write-Host "  posf status           Checks port and service statuses`n"
    }
}
