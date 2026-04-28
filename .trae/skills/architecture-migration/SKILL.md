---
name: "architecture-migration"
description: "Automates code architecture migration with coupling analysis and progressive file movement. Invoke when restructuring project directories, migrating code between projects, or adjusting architecture according to new specifications."
---

# Architecture Migration Skill

## Purpose

This skill automates the complex process of migrating code architecture while:
- Analyzing code coupling relationships
- Ensuring zero data loss
- Providing progressive verification
- Maintaining compilation integrity
- Reducing manual error risk

## When to Invoke

Use this skill when:
- User needs to restructure project directories
- Migrating code between projects according to new architecture documents
- Adjusting namespace organization
- Moving files from old structure to new structure
- User mentions "架构迁移", "目录调整", "重构目录", "迁移代码"

## Migration Process

### Phase 1: Pre-Migration Preparation

#### 1.1 Git Safety Check
```powershell
# Check if working directory is clean
git status

# If not clean, commit current changes first
git add .
git commit -m "chore: backup before architecture migration"
```

**Validation**: Must have clean working directory before proceeding.

#### 1.2 Architecture Document Analysis
- Read the target architecture specification (e.g., `架构要求.md`)
- Extract the target directory tree structure
- Identify all projects and their relationships
- Map old structure to new structure

### Phase 2: Coupling Analysis

#### 2.1 Build Dependency Graph
```powershell
# Analyze project references
dotnet list package
dotnet list reference

# For each project, identify:
# - Referenced projects (dependencies)
# - Referenced by projects (dependents)
# - Shared files/code
```

#### 2.2 Generate Migration Order
Based on coupling analysis, determine migration order:
1. **Low coupling projects first** (few dependencies)
2. **Foundation projects** (Common, Contracts)
3. **Dependent projects** (Host, Plugins)

**Output**: Migration queue with dependency order

### Phase 3: Directory Structure Creation

#### 3.1 Backup Old Structure
```powershell
# For each project to migrate:
Rename-Item -Path "src\OldProject" -NewName "src\OldProject_old"
```

#### 3.2 Create New Structure
```powershell
# Create new directories according to architecture spec
New-Item -ItemType Directory -Path "src\NewProject"
New-Item -ItemType Directory -Path "src\NewProject\SubDirectory1"
New-Item -ItemType Directory -Path "src\NewProject\SubDirectory2"
```

#### 3.3 Initialize Projects
```powershell
# Create project files
dotnet new classlib -n NewProject -o src\NewProject

# Add necessary references
dotnet add src\NewProject\NewProject.csproj reference src\Common\Common.csproj
```

#### 3.4 Git Checkpoint
```powershell
git add .
git commit -m "chore: create new directory structure"
```

### Phase 4: Progressive File Migration

#### 4.1 Create Migration Map
Generate a structured mapping file (JSON format):

```json
{
  "migrations": [
    {
      "source": "src/OldProject_old/Service.cs",
      "destination": "src/NewProject/Services/Service.cs",
      "type": "cut"
    },
    {
      "source": "src/OldProject_old/Models/",
      "destination": "src/NewProject/Models/",
      "type": "cut"
    }
  ]
}
```

#### 4.2 Migration Rules
- **ALWAYS use Move-Item (cut), never Copy-Item**
- **One project at a time**
- **Do NOT modify file contents during migration**
- **Do NOT change namespaces yet**

#### 4.3 Migration Loop
For each project in migration queue:

```powershell
# Step 1: Move files according to map
Move-Item -Path "src\OldProject_old\*" -Destination "src\NewProject\"

# Step 2: Attempt compilation
dotnet build src\NewProject\NewProject.csproj

# Step 3: Analyze errors
if (build fails) {
    # Check if errors are due to:
    # A) Missing files not yet migrated -> Continue migration
    # B) Code issues -> STOP and ask user
    
    # Decision logic:
    $errors = dotnet build 2>&1
    if ($errors -match "missing file|not found") {
        Write-Host "Missing files detected, continue migration"
        # Identify which files are missing
        # Update migration map
        # Continue with next batch
    } else {
        Write-Error "Build errors not related to missing files"
        # Ask user for decision
        break
    }
}

# Step 4: Verify partial success
if (build succeeds) {
    Write-Host "Migration batch successful"
    git add .
    git commit -m "chore: migrate [ProjectName] files"
}
```

#### 4.4 Retry Policy
- **Only ONE retry allowed** for the entire migration
- If retry fails, stop and ask user for decision
- Document all errors and context

### Phase 5: Namespace Adjustment

#### 5.1 Pre-conditions
- ALL projects must compile successfully
- ALL files must be migrated
- No files remaining in `_old` directories

#### 5.2 Global Using Extraction
```powershell
# Script to extract and consolidate using statements
$sourceFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

# Collect all using statements
$allUsings = @{}
foreach ($file in $sourceFiles) {
    $content = Get-Content $file.FullName
    $usings = $content | Select-String "^using\s+" | ForEach-Object { $_.Line.Trim() }
    foreach ($using in $usings) {
        if (-not $allUsings.ContainsKey($using)) {
            $allUsings[$using] = @()
        }
        $allUsings[$using] += $file.FullName
    }
}

# Generate GlobalUsings.cs
$globalUsings = $allUsings.Keys | Sort-Object
$globalUsingsContent = $globalUsings | ForEach-Object { $_ }
```

#### 5.3 Create GlobalUsings.cs
For each project, create/update `GlobalUsings.cs`:

```csharp
// Global using statements for [ProjectName]
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
// ... other common usings
```

#### 5.4 Remove Local Using Statements
```powershell
# Remove using statements from individual files
$sourceFiles = Get-ChildItem -Path "src\NewProject" -Filter "*.cs" -Recurse -Exclude "GlobalUsings.cs"

foreach ($file in $sourceFiles) {
    $content = Get-Content $file.FullName
    $newContent = $content | Where-Object { $_ -notmatch "^using\s+" }
    $newContent | Set-Content $file.FullName
}
```

#### 5.5 Update Namespace Declarations
```powershell
# Update namespace to match new directory structure
# OldProject.Services -> NewProject.Services
```

### Phase 6: Cleanup and Verification

#### 6.1 Check for Leftover Files
```powershell
# Check all _old directories
$oldDirs = Get-ChildItem -Path "src" -Directory -Filter "*_old"

foreach ($dir in $oldDirs) {
    $remainingFiles = Get-ChildItem -Path $dir.FullName -File -Recurse
    if ($remainingFiles.Count -gt 0) {
        Write-Warning "Files remaining in $($dir.Name):"
        $remainingFiles | ForEach-Object { Write-Host "  $($_.FullName)" }
        
        # Ask user: Continue migration or delete?
        $decision = Read-Host "Continue migrating these files? (Y/N)"
        if ($decision -eq 'Y') {
            # Add to migration map and continue
        }
    }
}
```

#### 6.2 Remove _old Directories
```powershell
# Only after confirming no files remain
foreach ($dir in $oldDirs) {
    if ((Get-ChildItem -Path $dir.FullName -File -Recurse).Count -eq 0) {
        Remove-Item -Path $dir.FullName -Recurse -Force
    }
}
```

#### 6.3 Final Build Verification
```powershell
# Build entire solution
dotnet build

# Run tests if available
dotnet test
```

#### 6.4 Final Git Commit
```powershell
git add .
git commit -m "refactor: complete architecture migration to new structure"
```

## Error Handling

### Build Failure During Migration

**Scenario**: Compilation fails after moving files

**Actions**:
1. Analyze error messages
2. Categorize errors:
   - Missing file references → Continue migration
   - Missing project references → Add references
   - Code incompatibility → Ask user
3. If cannot resolve automatically, STOP and ask user

### Namespace Conflicts

**Scenario**: Duplicate namespace after consolidation

**Actions**:
1. Use namespace aliases in GlobalUsings.cs
2. Example: `global using MyService = OldProject.Services.MyService;`

### Circular Dependencies

**Scenario**: Projects reference each other

**Actions**:
1. Identify circular dependency chain
2. Suggest introducing Contracts project
3. Ask user for approval before restructuring

## Coupling Analysis Tools

### Static Analysis
```powershell
# Analyze file dependencies using grep
grep -r "using OldProject" src/ --include="*.cs"

# Find all references to a specific class
grep -r "ClassName" src/ --include="*.cs"
```

### Dependency Visualization
Generate a simple dependency graph:
```
Common (no dependencies)
  ↑
Contracts (no dependencies)
  ↑
McpHost (depends on: Common, Contracts, McpProtocol)
  ↑
Plugins/* (depends on: Common, Contracts)
```

## Migration Map Template

```json
{
  "version": "1.0",
  "timestamp": "2024-01-01T00:00:00Z",
  "source_root": "src",
  "target_root": "src",
  "migrations": [
    {
      "id": 1,
      "source_project": "OldProject",
      "target_project": "NewProject",
      "files": [
        {
          "source": "Services/UserService.cs",
          "destination": "Services/UserService.cs",
          "namespace_old": "OldProject.Services",
          "namespace_new": "NewProject.Services"
        }
      ],
      "dependencies": ["Common", "Contracts"],
      "status": "pending"
    }
  ]
}
```

## Best Practices

1. **Always backup with git before starting**
2. **Move one project at a time**
3. **Compile after each batch**
4. **Don't modify content during migration**
5. **Only adjust namespaces after all files moved**
6. **Keep migration map as source of truth**
7. **Document all decisions and errors**
8. **Use PowerShell Move-Item, never manual copy-paste**

## Integration with Project Rules

This skill respects:
- `.trae/rules/MyMemoryRules.md` - Check for past migration issues
- `.trae/rules/项目铁律.md` - AOT compatibility, no JSON strings
- `架构要求.md` - Target architecture specification

## Output Artifacts

After migration, generate:
1. `migration-log.txt` - Detailed log of all operations
2. `migration-map.json` - Final migration mapping
3. `coupling-analysis.txt` - Dependency analysis results
4. `build-report.txt` - Compilation results at each stage

## Safety Guarantees

- ✅ Zero data loss (git backup + _old directories)
- ✅ Progressive verification (compile after each step)
- ✅ Rollback capability (git commits at each phase)
- ✅ Transparent logging (all operations documented)
- ✅ User control (stop and ask on critical decisions)
