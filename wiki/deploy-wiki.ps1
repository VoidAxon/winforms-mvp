# PowerShell script to deploy wiki content to GitHub
# Usage: .\deploy-wiki.ps1

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  WinForms MVP Framework - Wiki Deployer" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$wikiRepoUrl = "https://github.com/VoidAxon/winforms-mvp.wiki.git"
$tempDir = "$env:TEMP\winforms-mvp-wiki-deploy"
$wikiSourceDir = $PSScriptRoot  # Directory where this script is located

# Step 1: Clean up any previous deployment
if (Test-Path $tempDir) {
    Write-Host "[1/5] Cleaning up previous deployment..." -ForegroundColor Yellow
    Remove-Item -Path $tempDir -Recurse -Force
} else {
    Write-Host "[1/5] No previous deployment found" -ForegroundColor Green
}

# Step 2: Clone wiki repository
Write-Host "[2/5] Cloning wiki repository..." -ForegroundColor Yellow
try {
    git clone $wikiRepoUrl $tempDir
    if ($LASTEXITCODE -ne 0) {
        throw "Git clone failed. Make sure Wiki is enabled in GitHub Settings."
    }
    Write-Host "    Wiki repository cloned successfully" -ForegroundColor Green
} catch {
    Write-Host "    ERROR: Failed to clone wiki repository" -ForegroundColor Red
    Write-Host "    Make sure Wiki is enabled in GitHub repository settings" -ForegroundColor Red
    Write-Host "    Go to: https://github.com/VoidAxon/winforms-mvp/settings" -ForegroundColor Yellow
    exit 1
}

# Step 3: Copy wiki content
Write-Host "[3/5] Copying wiki content..." -ForegroundColor Yellow
$wikiFiles = Get-ChildItem -Path $wikiSourceDir -Filter "*.md" |
    Where-Object { $_.Name -ne "DEPLOY.md" -and $_.Name -ne "README.md" }

if ($wikiFiles.Count -eq 0) {
    Write-Host "    ERROR: No wiki markdown files found" -ForegroundColor Red
    exit 1
}

foreach ($file in $wikiFiles) {
    Copy-Item -Path $file.FullName -Destination $tempDir -Force
    Write-Host "    Copied: $($file.Name)" -ForegroundColor Green
}

# Step 4: Commit changes
Write-Host "[4/5] Committing changes..." -ForegroundColor Yellow
Push-Location $tempDir
try {
    git add *.md
    $commitMessage = "docs: Update wiki pages - $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    git commit -m $commitMessage

    if ($LASTEXITCODE -eq 0) {
        Write-Host "    Changes committed successfully" -ForegroundColor Green
    } else {
        Write-Host "    No changes to commit (wiki is already up to date)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "    ERROR: Failed to commit changes" -ForegroundColor Red
    Pop-Location
    exit 1
}

# Step 5: Push to GitHub
Write-Host "[5/5] Pushing to GitHub..." -ForegroundColor Yellow
try {
    git push origin master

    if ($LASTEXITCODE -eq 0) {
        Write-Host "    Successfully pushed to GitHub!" -ForegroundColor Green
    } else {
        Write-Host "    Push completed (or no changes to push)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "    ERROR: Failed to push to GitHub" -ForegroundColor Red
    Write-Host "    You may need to authenticate with GitHub" -ForegroundColor Yellow
    Pop-Location
    exit 1
}

Pop-Location

# Cleanup
Write-Host ""
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Path $tempDir -Recurse -Force

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "View your wiki at: https://github.com/VoidAxon/winforms-mvp/wiki" -ForegroundColor Yellow
Write-Host ""

# Open wiki in browser
$openBrowser = Read-Host "Open wiki in browser? (Y/N)"
if ($openBrowser -eq "Y" -or $openBrowser -eq "y") {
    Start-Process "https://github.com/VoidAxon/winforms-mvp/wiki"
}
