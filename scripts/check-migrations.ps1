[CmdletBinding()]
param(
    [string] $OutputPath = "artifacts/efcore-migrations.sql"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $repositoryRoot "backend"
$infrastructureProject = Join-Path $backendRoot "PermissionSystem.Infrastructure/PermissionSystem.Infrastructure.csproj"
$startupProject = Join-Path $backendRoot "PermissionSystem.Api/PermissionSystem.Api.csproj"
$outputFile = Join-Path $repositoryRoot $OutputPath

if (-not (Test-Path -LiteralPath $infrastructureProject -PathType Leaf)) {
    throw "Infrastructure project not found: $infrastructureProject"
}

Push-Location $backendRoot
try {
    dotnet tool restore
    $outputDirectory = Split-Path -Parent $outputFile
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    dotnet ef migrations script 0 --idempotent `
        --project $infrastructureProject `
        --startup-project $startupProject `
        --context AppDbContext `
        --output $outputFile
} finally {
    Pop-Location
}

$migrationFiles = Get-ChildItem (Join-Path $backendRoot "PermissionSystem.Infrastructure/Data/Migrations") -Filter "*.cs" |
    Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.Name -ne "AppDbContextModelSnapshot.cs" }
foreach ($migration in $migrationFiles) {
    $content = Get-Content -LiteralPath $migration.FullName -Raw
    if ($content -notmatch "protected override void Up\s*\(MigrationBuilder migrationBuilder\)" -or
        $content -notmatch "protected override void Down\s*\(MigrationBuilder migrationBuilder\)") {
        throw "Migration is missing Up/Down methods: $($migration.Name)"
    }
}

if ((Get-Item -LiteralPath $outputFile).Length -eq 0) {
    throw "Generated migration script is empty: $outputFile"
}

Write-Host "Migration script generated and migration Up/Down checks passed: $outputFile"
