#!/usr/bin/env powershell
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$packagesToClean = @("McpProtocol", "McpProtocol.Contracts")
$nugetCachePath = "$env:USERPROFILE\.nuget\packages"
$localNugetPath = ".\nuget"

Write-Host "Cleaning NuGet cache for local packages..." -ForegroundColor Cyan

$cleanedCount = 0

foreach ($package in $packagesToClean) {
    $packageCachePath = Join-Path $nugetCachePath $package.ToLowerInvariant()
    
    if (Test-Path $packageCachePath) {
        Write-Host "  Removing: $packageCachePath" -ForegroundColor Yellow
        Remove-Item -Path $packageCachePath -Recurse -Force -ErrorAction SilentlyContinue
        $cleanedCount++
    }
    
    $localPackagePath = Join-Path $localNugetPath $package.ToLowerInvariant()
    if (Test-Path $localPackagePath) {
        Write-Host "  Removing local: $localPackagePath" -ForegroundColor Yellow
        Remove-Item -Path $localPackagePath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($cleanedCount -eq 0) {
    Write-Host "  No cache entries found to clean." -ForegroundColor Gray
}

Write-Host "`nClearing NuGet HTTP cache..." -ForegroundColor Cyan
dotnet nuget locals http-cache --clear 2>$null

Write-Host "`nCache cleanup completed!" -ForegroundColor Green
Write-Host "  Packages cleaned: $cleanedCount" -ForegroundColor Cyan
