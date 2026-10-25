<#
.SYNOPSIS
  Finalises the Week 2 ApexPost refactor: removes obsolete Week 1 files, verifies the build and
  tests, commits layer by layer on a feature branch, pushes, and opens a pull request.

.USAGE
  From the repository root in PowerShell:
      .\scripts\finalize-week2.ps1
  Optional:
      .\scripts\finalize-week2.ps1 -Branch feature/my-name -Base main -SkipPush
#>
param(
    [string]$Branch = "feature/week2-apexpost-counter-platform",
    [string]$Base = "main",
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

$trailer = @"
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0122KPm2RMziTSmn7xFdWppA
"@

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)." }
}

function Commit-Paths([string[]]$Paths, [string]$Message) {
    $existing = $Paths | Where-Object { (Test-Path $_) -or (git ls-files -- $_) }
    if (-not $existing) { return }
    git add -A -- $existing
    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { Write-Host "Nothing to commit for: $Message"; return }
    git commit -q -m $Message -m $trailer
    Write-Host "Committed: $Message" -ForegroundColor Green
}

# 1. Feature branch
$current = git rev-parse --abbrev-ref HEAD
if ($current -ne $Branch) {
    Invoke-Checked { git checkout -b $Branch } "Creating branch $Branch"
}

# 2. Obsolete Week 1 / EF Core files that the new code replaces
$obsolete = @(
    "ResourceManagementSystem",                                   # TechNova console project (not in the solution any more)
    "ResourceManagementSystem.Repositories\BaseEntityRepository.cs", # EF Core repository: no ORM this week
    "ResourceManagementSystem.Database\ApplicationDbContext.cs",     # EF Core DbContext
    "ResourceManagementSystem.Database\JSONs\audit-log.json",        # runtime audit log now lives in App_Data (git-ignored)
    "ResourceManagementSystem.Database\JSONs\AUDIT-SAMPLE.json",     # moved to the repository root
    "ResourceManagementSystem.MVC\Views\Shared\_Layout.cshtml.css",  # Bootstrap-era CSS isolation file
    "ResourceManagementSystem.MVC\Views\Home\Index.cshtml",          # Home/Index now redirects to the branch dashboard
    "ResourceManagementSystem.MVC\App_Data"                          # local runtime data, never committed
)
foreach ($path in $obsolete) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Host "Removed $path"
    }
}

# 3. Clean stale build output, then build (warnings are errors) and test
Get-ChildItem -Directory -Recurse -Force -Include bin, obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Invoke-Checked { dotnet build .\ResourceManagementSystem.slnx } "Build"
Invoke-Checked { dotnet test .\ResourceManagementSystem.slnx --no-build } "Tests"

# 4. Commit layer by layer
Commit-Paths @("ResourceManagementSystem", "ResourceManagementSystem.Repositories\BaseEntityRepository.cs", "ResourceManagementSystem.Database\ApplicationDbContext.cs") `
    "chore: remove TechNova console project and EF Core leftovers"
Commit-Paths @("Directory.Build.props", ".gitignore", "ResourceManagementSystem.Models", "ResourceManagementSystem.Database") `
    "feat(domain): ApexPost value objects, entities and realistic seed data"
Commit-Paths @("ResourceManagementSystem.Repositories") `
    "feat(repositories): async JSON repository with snapshots, SemaphoreSlim, atomic writes and specifications"
Commit-Paths @("ResourceManagementSystem.Services") `
    "feat(services): counter business rules, pricing strategy, events, caching, courier simulation and reports"
Commit-Paths @("ResourceManagementSystem.Utilities") `
    "feat(utilities): middleware pipeline, audit/validation/performance/domain-exception filters"
Commit-Paths @("ResourceManagementSystem.MVC") `
    "feat(mvc): counter screens, cookie auth policies, rate limiting, CORS and health checks"
Commit-Paths @("ResourceManagementSystem.Tests") `
    "test: business rule unit tests and WebApplicationFactory integration tests"
Commit-Paths @("README.md", "DESIGN.md", "AUDIT-SAMPLE.json", "ApexPost-Postman-Collection.json", "ResourceManagementSystem.slnx", "docs", "scripts") `
    "docs: README, DESIGN with Week 2 ADRs, audit sample, Postman collection and screenshots"

git status --short

# 5. Push and open the pull request
if ($SkipPush) { Write-Host "Skipping push. Run: git push -u origin $Branch"; return }
Invoke-Checked { git push -u origin $Branch } "Push"

$title = "Week 2: ApexPost Counter Platform (MVC, middleware, filters, async)"
if (Get-Command gh -ErrorAction SilentlyContinue) {
    gh pr create --base $Base --head $Branch --title $title --body-file .\scripts\week2-pr-body.md
} else {
    Write-Host "GitHub CLI not found. Open a PR from '$Branch' into '$Base' on GitHub with the title:" -ForegroundColor Yellow
    Write-Host "  $title"
    Write-Host "and paste scripts\week2-pr-body.md as the description."
}
