[CmdletBinding()]
param(
    [ValidateSet('Run', 'Compare', 'ReviewTemplate', 'SnapshotTemplate')][string]$Command = 'Run',
    [ValidateSet('Offline', 'Live')][string]$Mode = 'Offline',
    [string]$Suite,
    [string]$Snapshot,
    [string]$Output,
    [string]$LiveConfig,
    [string]$Baseline,
    [string]$Candidate,
    [string]$Review,
    [string]$BaselineReview,
    [switch]$RequireReleaseGate,
    [switch]$NoBuild,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'backend/PermissionSystem.AiEvaluations/PermissionSystem.AiEvaluations.csproj'
if (-not $Output) { $Output = Join-Path $repositoryRoot 'artifacts/ai-evaluations' }
$dotnetArgs = @('run', '--project', $project, '--configuration', $Configuration)
if ($NoBuild) { $dotnetArgs += '--no-build' }
$entryCommand = @{ Run = 'run'; Compare = 'compare'; ReviewTemplate = 'review-template'; SnapshotTemplate = 'snapshot-template' }[$Command]
$dotnetArgs += @('--', '--command', $entryCommand, '--root', $repositoryRoot, '--output', $Output)
if ($Command -eq 'Run') { $dotnetArgs += @('--mode', $Mode.ToLowerInvariant()) }
$paths = @{ snapshot = $Snapshot; suite = $Suite; 'live-config' = $LiveConfig; baseline = $Baseline; candidate = $Candidate; review = $Review; 'baseline-review' = $BaselineReview }
foreach ($key in $paths.Keys) { if ($paths[$key]) { $dotnetArgs += @("--$key", $paths[$key]) } }
if ($RequireReleaseGate) { $dotnetArgs += '--require-release-gate' }
& dotnet @dotnetArgs
exit $LASTEXITCODE
