<#
.SYNOPSIS
  Finishes the Week 2 clean-up. It removes the Week 1 code and the old project names, checks the
  build and tests, commits, and pushes.

.USAGE
  Close Visual Studio first, because it keeps files locked. Then, from the repository root:
      .\scripts\finalize-week2.ps1
  Optional parameters:
      .\scripts\finalize-week2.ps1 -Branch feature/week2-apexpost -SkipPush
  When -Branch is left out, the script commits to the branch you are on.
#>
param(
    [string]$Branch = "",
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
$oldName = "Resource" + "ManagementSystem"   # built from parts so the repository never contains the old name
Set-Location (Split-Path $PSScriptRoot -Parent)

$trailer = @"
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0122KPm2RMziTSmn7xFdWppA
"@

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)." }
}

if (-not (Test-Path ".\ApexPost.slnx")) {
    throw "ApexPost.slnx was not found. Copy the ApexPost.* folders into the repository root first."
}

# 1. Optional feature branch
if ($Branch) {
    $current = git rev-parse --abbrev-ref HEAD
    if ($current -ne $Branch) { Invoke-Checked { git checkout -b $Branch } "Creating branch $Branch" }
}

# 2. Remove everything that still uses the old name or belongs to Week 1:
#    - the Week 1 console project (EF Core, Employee/ClientProject)
#    - the old layer projects with the Week 1 prefix, now ApexPost.* (same layers, new names)
#    - the old solution file
#    The old folders also contain the placeholders ApplicationDbContext.cs and BaseEntityRepository.cs,
#    the Week 1 JSONs\audit-log.json, JSONs\AUDIT-SAMPLE.json, _Layout.cshtml.css and the .csproj.user file.
$old = @(Get-ChildItem -Force -Directory -Filter "$oldName*" | ForEach-Object Name) +
       @(Get-ChildItem -Force -File -Filter "$oldName*" | ForEach-Object Name)
foreach ($path in $old) {
    git rm -r -q --ignore-unmatch -- $path | Out-Null      # tracked files: removed from git and disk
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }  # untracked leftovers (bin, obj, App_Data, *.user)
    Write-Host "Removed $path"
}
if (Test-Path ".vs") { Remove-Item ".vs" -Recurse -Force -ErrorAction SilentlyContinue }  # IDE cache that points at the old solution

# 3. Fail fast if the old name survives anywhere in tracked or new source files
$leftovers = git grep -l -I $oldName -- .
$untracked = git ls-files --others --exclude-standard | Where-Object { $_ -match $oldName }
if ($leftovers -or $untracked) {
    throw "The old name is still present in: $($leftovers + $untracked -join ', ')"
}

# 4. Clean build (warnings are errors) and tests
Get-ChildItem -Directory -Recurse -Force -Include bin, obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Invoke-Checked { dotnet build .\ApexPost.slnx } "Build"
Invoke-Checked { dotnet test .\ApexPost.slnx --no-build } "Tests"

# 5. Commit. Git recognises the moves as renames, so file history is kept.
git add -A
git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Host "Nothing to commit."; return }
git commit -q -m "refactor: rename layer projects to ApexPost.* and remove Week 1 leftovers" `
    -m "Same layered architecture (Models, Database, Repositories, Services, Utilities, MVC, Tests); only the names change. Removes the Week 1 console project, the placeholder ApplicationDbContext.cs and BaseEntityRepository.cs, and the Week 1 audit-log.json." `
    -m $trailer
Write-Host "Committed." -ForegroundColor Green
git show --stat --oneline -M HEAD | Select-Object -First 15

# 6. Push
$current = git rev-parse --abbrev-ref HEAD
if ($SkipPush) { Write-Host "Skipping push. Run: git push -u origin $current"; return }
Invoke-Checked { git push -u origin $current } "Push"
