#!/usr/bin/env powershell
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("patch", "minor", "major")]
    [string]$VersionBump = "patch"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Green
Write-Host "MCP-CLI Bridge Release" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$repoRoot = $PSScriptRoot

function Get-LocalVersion {
    $packageJson = Get-Content "$repoRoot\package.json" -Raw | ConvertFrom-Json
    return $packageJson.version
}

function Get-NpmLatestVersion {
    param([string]$packageName)
    
    try {
        $result = npm view $packageName version 2>$null
        if ($LASTEXITCODE -eq 0 -and $result) {
            return $result.Trim()
        }
    } catch {}
    
    return $null
}

function Update-Version {
    param([string]$currentVersion, [string]$bumpType)
    
    $parts = $currentVersion.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    
    switch ($bumpType) {
        "major" { $major++; $minor = 0; $patch = 0 }
        "minor" { $minor++; $patch = 0 }
        "patch" { $patch++ }
    }
    
    return "$major.$minor.$patch"
}

function Update-PackageJson {
    param([string]$newVersion)
    
    $packageJsonPath = "$repoRoot\package.json"
    $content = Get-Content $packageJsonPath -Raw
    $content = $content -replace '"version":\s*"[^"]*"', "`"version`": `"$newVersion`""
    Set-Content $packageJsonPath $content -NoNewline
}

$gitStatus = git status --porcelain 2>$null
if ($gitStatus) {
    Write-Error "Git working directory is not clean! Please commit or stash changes first."
    Write-Host "Uncommitted changes:" -ForegroundColor Yellow
    Write-Host $gitStatus
    exit 1
}

$localVersion = Get-LocalVersion
$packageName = "@jingjingbox/mcp-cli-bridge"
$npmLatestVersion = Get-NpmLatestVersion $packageName

Write-Host "`n[Version Check]" -ForegroundColor Cyan
Write-Host "  Local package.json: v$localVersion" -ForegroundColor Gray
Write-Host "  npm registry latest: $(if($npmLatestVersion){'v'+$npmLatestVersion}else{'(not published)'})" -ForegroundColor Gray

$baseVersion = if ($npmLatestVersion) { $npmLatestVersion } else { $localVersion }
$newVersion = Update-Version $baseVersion $VersionBump

Write-Host "`n[Version] $baseVersion -> $newVersion ($VersionBump)" -ForegroundColor Cyan

Update-PackageJson $newVersion
Write-Host "  Updated package.json" -ForegroundColor Gray

Write-Host "`n[Git] Committing and tagging..." -ForegroundColor Cyan

git add "$repoRoot\package.json"
git commit -m "release: v$newVersion"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Git commit failed!"
    exit 1
}

git tag "v$newVersion"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Git tag failed!"
    git reset --hard HEAD~1
    exit 1
}

Write-Host "`n[Push] Pushing to remote..." -ForegroundColor Cyan
git push origin main
git push origin "v$newVersion"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Release v$newVersion triggered!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nCI/CD will now:" -ForegroundColor Cyan
Write-Host "  1. Build and test" -ForegroundColor Gray
Write-Host "  2. Publish to npm" -ForegroundColor Gray
Write-Host "  3. Create GitHub Release" -ForegroundColor Gray
Write-Host "`nMonitor: https://github.com/JJbox/memory/actions" -ForegroundColor Yellow
