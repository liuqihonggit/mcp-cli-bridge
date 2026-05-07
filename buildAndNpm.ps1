#!/usr/bin/env powershell
# Build and publish to npm

$ErrorActionPreference = "Stop"

Write-Host "Building MCP-CLI Bridge (npm)..." -ForegroundColor Green

& "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Step: Copy npm package files
Write-Host "`n[Copy] npm package files..." -ForegroundColor Cyan
Copy-Item "$PSScriptRoot\package.json" "$PSScriptRoot\publish\" -Force
Copy-Item "$PSScriptRoot\index.js" "$PSScriptRoot\publish\" -Force
Copy-Item "$PSScriptRoot\README.md" "$PSScriptRoot\publish\" -Force

# Publish to npm
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Ready to publish to npm!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nTo publish, run: cd publish; npm publish" -ForegroundColor Yellow
