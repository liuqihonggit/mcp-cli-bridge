#!/usr/bin/env powershell
# Build script for MCP-CLI Bridge npm package

$ErrorActionPreference = "Stop"

Write-Host "Building MCP-CLI Bridge..." -ForegroundColor Green

# Step 1: Auto-discover and pack all local NuGet packages in lib/
Write-Host "`n[Packing] Local NuGet packages..." -ForegroundColor Cyan

$localNugetPath = "$PSScriptRoot\nuget"
if (-not (Test-Path $localNugetPath)) {
    New-Item -ItemType Directory -Path $localNugetPath -Force | Out-Null
}

function Pack-NugetProject {
    param([string]$ProjectPath, [string]$ProjectName)

    Write-Host "  Packing $ProjectName..." -ForegroundColor Gray
    dotnet pack $ProjectPath -c Release -o $localNugetPath --no-build

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Building $ProjectName first..." -ForegroundColor Yellow
        dotnet build $ProjectPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "$ProjectName build failed!" }
        dotnet pack $ProjectPath -c Release -o $localNugetPath
    }

    if ($LASTEXITCODE -ne 0) { throw "$ProjectName pack failed!" }
}

# 自动发现 lib/ 下所有 .csproj，按名称排序（Contracts 自然排在主项目前面）
$libProjects = @(Get-ChildItem -Path "$PSScriptRoot\lib" -Recurse -Filter "*.csproj" -File |
    Sort-Object FullName)

foreach ($project in $libProjects) {
    $projectName = $project.BaseName
    Pack-NugetProject -ProjectPath $project.FullName -ProjectName $projectName
}

Write-Host "  All local NuGet packages packed successfully. ($($libProjects.Count) projects)" -ForegroundColor Green

# Step 2: Clear NuGet cache for local packages
Write-Host "`n[Cache] Clearing NuGet cache..." -ForegroundColor Cyan
& "$PSScriptRoot\scripts\clear-nuget-cache.ps1"

# Step 3: Clean publish directory
if (Test-Path "$PSScriptRoot\publish") {
    Get-ChildItem "$PSScriptRoot\publish\*" -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Path "$PSScriptRoot\publish" -Force | Out-Null
New-Item -ItemType Directory -Path "$PSScriptRoot\publish\Plugins\MemoryCli" -Force | Out-Null
New-Item -ItemType Directory -Path "$PSScriptRoot\publish\Plugins\FileReaderCli" -Force | Out-Null

# Step 4: Build McpHost
Write-Host "`n[Build] McpHost (AOT)..." -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\src\McpHost\McpHost.csproj" `
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

# Step 5: Build MemoryCli
Write-Host "`n[Build] MemoryCli (AOT)..." -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\src\Plugins\MemoryCli\MemoryCli.csproj" `
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

# Step 6: Build FileReaderCli
Write-Host "`n[Build] FileReaderCli (AOT)..." -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\src\Plugins\FileReaderCli\FileReaderCli.csproj" `
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

# Step 7: Copy npm package files
Write-Host "`n[Copy] npm package files..." -ForegroundColor Cyan
Copy-Item "$PSScriptRoot\package.json" "$PSScriptRoot\publish\" -Force
Copy-Item "$PSScriptRoot\index.js" "$PSScriptRoot\publish\" -Force
Copy-Item "$PSScriptRoot\README.md" "$PSScriptRoot\publish\" -Force

# Copy CLI documentation files to Plugins subdirectories
Copy-Item "$PSScriptRoot\src\Plugins\MemoryCli\CLI说明.md" "$PSScriptRoot\publish\Plugins\MemoryCli\CLI说明.md" -Force
Copy-Item "$PSScriptRoot\src\Plugins\FileReaderCli\CLI说明.md" "$PSScriptRoot\publish\Plugins\FileReaderCli\CLI说明.md" -Force

# Verify outputs
$requiredFiles = @(
    "$PSScriptRoot\publish\McpHost.exe",
    "$PSScriptRoot\publish\Plugins\MemoryCli\MemoryCli.exe",
    "$PSScriptRoot\publish\Plugins\FileReaderCli\FileReaderCli.exe"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Error "Missing required file: $file"
        exit 1
    }
}

# Show results
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "`nPublished files:" -ForegroundColor Cyan
Get-ChildItem "$PSScriptRoot\publish\*" -File | ForEach-Object {
    $size = "{0:N0}" -f $_.Length
    Write-Host "  $($_.Name) ($size bytes)"
}

Write-Host "`nPlugins:" -ForegroundColor Cyan
Get-ChildItem "$PSScriptRoot\publish\Plugins" -Directory | ForEach-Object {
    $pluginName = $_.Name
    Write-Host "  [$pluginName]" -ForegroundColor Yellow
    Get-ChildItem $_.FullName -File | ForEach-Object {
        $size = "{0:N0}" -f $_.Length
        Write-Host "    $($_.Name) ($size bytes)"
    }
}

Write-Host "`nPackage size:" -ForegroundColor Cyan
$hostSize = (Get-ChildItem "$PSScriptRoot\publish\*" -File | Measure-Object -Property Length -Sum).Sum
$pluginsSize = (Get-ChildItem "$PSScriptRoot\publish\Plugins" -Recurse -File | Measure-Object -Property Length -Sum).Sum
$totalSize = $hostSize + $pluginsSize
$sizeMB = "{0:N2}" -f ($totalSize / 1MB)
Write-Host "  Total: $sizeMB MB"

Write-Host "`nTo publish to npm, run: npm publish" -ForegroundColor Yellow
