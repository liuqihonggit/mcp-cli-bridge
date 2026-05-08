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
        $job = Start-Job -ScriptBlock {
            param($pkg)
            npm view $pkg version 2>$null
        } -ArgumentList $packageName
        
        $result = Wait-Job $job -Timeout 10 | Receive-Job $job
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        
        if ($result) {
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

function Update-DirectoryBuildProps {
    param([string]$newVersion)
    
    $propsPath = "$repoRoot\Directory.Build.props"
    $content = Get-Content $propsPath -Raw
    $content = $content -replace '<UnifiedVersion>[^<]*</UnifiedVersion>', "<UnifiedVersion>$newVersion</UnifiedVersion>"
    Set-Content $propsPath $content -NoNewline
}

function Invoke-GitPush {
    param([string]$remote, [string]$ref, [int]$timeoutSeconds = 60)
    
    $job = Start-Job -ScriptBlock {
        param($remote, $ref)
        git push $remote $ref 2>&1
    } -ArgumentList $remote, $ref
    
    $result = Wait-Job $job -Timeout $timeoutSeconds
    if (-not $result) {
        Remove-Job $job -Force
        Write-Host "  [TIMEOUT] git push $remote $ref" -ForegroundColor Yellow
        return $false
    }
    
    $output = Receive-Job $job
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FAILED] git push $remote $ref" -ForegroundColor Yellow
        return $false
    }
    
    Write-Host "  [OK] git push $remote $ref" -ForegroundColor Green
    return $true
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

Write-Host "`n[Version Check]" -ForegroundColor Cyan
Write-Host "  Local package.json: v$localVersion" -ForegroundColor Gray

$npmLatestVersion = Get-NpmLatestVersion $packageName
Write-Host "  npm registry latest: $(if($npmLatestVersion){'v'+$npmLatestVersion}else{'(timeout or not published)'})" -ForegroundColor Gray

$baseVersion = if ($npmLatestVersion) { $npmLatestVersion } else { $localVersion }
$newVersion = Update-Version $baseVersion $VersionBump

Write-Host "`n[Version] $baseVersion -> $newVersion ($VersionBump)" -ForegroundColor Cyan

Update-PackageJson $newVersion
Write-Host "  Updated package.json" -ForegroundColor Gray

Update-DirectoryBuildProps $newVersion
Write-Host "  Updated Directory.Build.props" -ForegroundColor Gray

Write-Host "`n[Git] Committing and tagging..." -ForegroundColor Cyan

git add "$repoRoot\package.json" "$repoRoot\Directory.Build.props"
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

Write-Host "`n[Push] Pushing to remotes (timeout: 60s each)..." -ForegroundColor Cyan

$pushSuccess = $false

# Push to origin (Gitee)
if (Invoke-GitPush "origin" "main" 60) {
    $pushSuccess = $true
}

if (Invoke-GitPush "origin" "v$newVersion" 60) {
    $pushSuccess = $true
}

# Push to github (GitHub) for CI/CD trigger
if (Invoke-GitPush "github" "main" 60) {
    $pushSuccess = $true
}

if (Invoke-GitPush "github" "v$newVersion" 60) {
    $pushSuccess = $true
}

if (-not $pushSuccess) {
    Write-Host "`n[WARNING] All push attempts failed, but tag is created locally" -ForegroundColor Yellow
    Write-Host "  You can manually push with:" -ForegroundColor Gray
    Write-Host "    git push origin main && git push origin v$newVersion" -ForegroundColor Gray
    Write-Host "    git push github main && git push github v$newVersion" -ForegroundColor Gray
}

Write-Host "`n[Cache] Cleaning local npm cache..." -ForegroundColor Cyan

$globalInstalled = npm list -g $packageName --depth=0 2>$null | Select-String $packageName
if ($globalInstalled) {
    Write-Host "  Uninstalling old global version..." -ForegroundColor Gray
    npm uninstall -g $packageName 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Global package removed" -ForegroundColor Green
    } else {
        Write-Host "  [SKIP] Global uninstall failed (may need admin)" -ForegroundColor Yellow
    }
}

npm cache clean --force 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  [OK] npm cache cleaned" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] Cache clean failed" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Release v$newVersion completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

if ($pushSuccess) {
    Write-Host "`nCI/CD will now:" -ForegroundColor Cyan
    Write-Host "  1. Build and test" -ForegroundColor Gray
    Write-Host "  2. Publish to npm" -ForegroundColor Gray
    Write-Host "  3. Create GitHub Release" -ForegroundColor Gray
    Write-Host "`nMonitor: https://github.com/liuqihonggit/mcp-cli-bridge/actions" -ForegroundColor Yellow
    Write-Host "`nAfter CI/CD publishes to npm, install with:" -ForegroundColor Cyan
    Write-Host "  npm install -g $packageName@latest" -ForegroundColor Gray
}
