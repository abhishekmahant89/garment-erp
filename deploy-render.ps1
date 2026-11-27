# ==============================================================================
# GarmentERP Render Cloud Deployment Helper Script
# ==============================================================================
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "    GARMENT STORE ERP - RENDER CLOUD DEPLOYMENT      " -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Check Git Status
$gitBranch = git branch --show-current
Write-Host "[1/3] Current Git Branch: $gitBranch" -ForegroundColor Yellow

# Check Render Blueprint File
if (Test-Path "render.yaml") {
    Write-Host "[2/3] render.yaml Blueprint: FOUND (Singapore Region, Free PostgreSQL + Docker API)" -ForegroundColor Green
} else {
    Write-Host "[2/3] render.yaml Blueprint: MISSING" -ForegroundColor Red
}

# Check Dockerfile
if (Test-Path "Dockerfile") {
    Write-Host "[3/3] Dockerfile: FOUND (Multi-stage .NET 10 Web API)" -ForegroundColor Green
} else {
    Write-Host "[3/3] Dockerfile: MISSING" -ForegroundColor Red
}

Write-Host ""
Write-Host "-----------------------------------------------------" -ForegroundColor Gray
Write-Host "HOW TO DEPLOY ON RENDER (STEP-BY-STEP):" -ForegroundColor Cyan
Write-Host "-----------------------------------------------------" -ForegroundColor Gray
Write-Host "1. Create a repository on GitHub (e.g. 'garment-erp')"
Write-Host "2. Link your local repo and push:"
Write-Host "   git remote add origin https://github.com/<YOUR_USERNAME>/garment-erp.git" -ForegroundColor White
Write-Host "   git push -u origin main" -ForegroundColor White
Write-Host ""
Write-Host "3. Open Render Dashboard: https://dashboard.render.com/"
Write-Host "4. Click [New +] -> [Blueprint]" -ForegroundColor Yellow
Write-Host "5. Select your GitHub repository and click [Apply]!" -ForegroundColor Green
Write-Host ""
Write-Host "Render will automatically provision PostgreSQL and deploy the Web Service!" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
