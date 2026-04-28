#!/usr/bin/env powershell
# Build script for MCP-CLI Bridge npm package

$ErrorActionPreference = "Stop"

# Clean publish directory first
if (Test-Path "publish") {
    Get-ChildItem "publish\*" -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Building MCP-CLI Bridge..." -ForegroundColor Green

New-Item -ItemType Directory -Path "publish" -Force | Out-Null
New-Item -ItemType Directory -Path "publish\Plugins\MemoryCli" -Force | Out-Null
New-Item -ItemType Directory -Path "publish\Plugins\FileReaderCli" -Force | Out-Null

# Build McpHost
Write-Host "Building McpHost (AOT)..." -ForegroundColor Cyan
dotnet publish src\McpHost\McpHost.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishAot=true `
    -p:PublishDir="$PSScriptRoot\publish"

if ($LASTEXITCODE -ne 0) {
    Write-Error "McpHost build failed!"
    exit 1
}

# Build MemoryCli
Write-Host "Building MemoryCli (AOT)..." -ForegroundColor Cyan
dotnet publish src\Plugins\MemoryCli\MemoryCli.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishAot=true `
    -p:PublishDir="$PSScriptRoot\publish\Plugins\MemoryCli"

if ($LASTEXITCODE -ne 0) {
    Write-Error "MemoryCli build failed!"
    exit 1
}

# Build FileReaderCli
Write-Host "Building FileReaderCli (AOT)..." -ForegroundColor Cyan
dotnet publish src\Plugins\FileReaderCli\FileReaderCli.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishAot=true `
    -p:PublishDir="$PSScriptRoot\publish\Plugins\FileReaderCli"

if ($LASTEXITCODE -ne 0) {
    Write-Error "FileReaderCli build failed!"
    exit 1
}

# Copy npm package files to publish
Write-Host "Copying npm package files..." -ForegroundColor Cyan
Copy-Item "package.json" "publish\" -Force
Copy-Item "index.js" "publish\" -Force
Copy-Item "README.md" "publish\" -Force

# Copy CLI documentation
Write-Host "Copying CLI documentation..." -ForegroundColor Cyan
Copy-Item "src\Plugins\MemoryCli\CLI说明.md" "publish\Plugins\MemoryCli\CLI说明.md" -Force
Copy-Item "src\Plugins\FileReaderCli\CLI说明.md" "publish\Plugins\FileReaderCli\CLI说明.md" -Force

# Verify outputs
$requiredFiles = @(
    "publish\McpHost.exe",
    "publish\Plugins\MemoryCli\MemoryCli.exe",
    "publish\Plugins\FileReaderCli\FileReaderCli.exe"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Error "Missing required file: $file"
        exit 1
    }
}

# Show results
Write-Host "`nBuild completed successfully!" -ForegroundColor Green
Write-Host "`nPublished files:" -ForegroundColor Cyan
Get-ChildItem "publish\*" -File | ForEach-Object {
    $size = "{0:N0}" -f $_.Length
    Write-Host "  $($_.Name) ($size bytes)"
}

Write-Host "`nPlugins:" -ForegroundColor Cyan
Get-ChildItem "publish\Plugins" -Directory | ForEach-Object {
    $pluginName = $_.Name
    Write-Host "  [$pluginName]" -ForegroundColor Yellow
    Get-ChildItem $_.FullName -File | ForEach-Object {
        $size = "{0:N0}" -f $_.Length
        Write-Host "    $($_.Name) ($size bytes)"
    }
}

Write-Host "`nPackage size:" -ForegroundColor Cyan
$hostSize = (Get-ChildItem "publish\*" -File | Measure-Object -Property Length -Sum).Sum
$pluginsSize = (Get-ChildItem "publish\Plugins" -Recurse -File | Measure-Object -Property Length -Sum).Sum
$totalSize = $hostSize + $pluginsSize
$sizeMB = "{0:N2}" -f ($totalSize / 1MB)
Write-Host "  Total: $sizeMB MB"

Write-Host "`nTo publish to npm, run: npm publish" -ForegroundColor Yellow
