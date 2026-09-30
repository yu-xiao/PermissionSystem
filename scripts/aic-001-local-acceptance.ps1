#requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Prepare')][switch] $PrepareOnly,
    [Parameter(Mandatory, ParameterSetName = 'Execute')][string] $ReviewedMigrationDirectory,
    [Parameter(ParameterSetName = 'Execute')][string] $RedisServerPath,
    [string] $LocalDbVersion = '17.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$previous = '20260831092535_AddMcpDatasetSchemaGovernanceP4'
$target = '20260924032728_AddAiUsageEstimates'
$runId = 'aic001_' + [Guid]::NewGuid().ToString('N')
$privateDirectory = Join-Path $root ".tools/aic-001/$runId"
$evidenceDirectory = Join-Path $root "artifacts/aic-001/$runId"
$null = New-Item -ItemType Directory -Path $privateDirectory, $evidenceDirectory
$children = [Collections.Generic.List[object]]::new()
$checks = [Collections.Generic.List[object]]::new()
$observations = @{}
$secrets = [Collections.Generic.List[string]]::new()
$instanceCreated = $false
$stage = 'Prerequisites'
$startedAt = [DateTimeOffset]::UtcNow
$failure = $null

function Assert-Check([string] $Name, [bool] $Condition) {
    $checks.Add([ordered]@{ name = $Name; passed = $Condition; at = [DateTimeOffset]::UtcNow.ToString('O') })
    if (-not $Condition) { throw "Check failed: $Name" }
    Write-Host "PASS $Name"
}

function New-Secret {
    $value = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $secrets.Add($value)
    return $value
}

function Protect-Text([string] $Text) {
    foreach ($secret in $secrets) {
        if ($secret) { $Text = $Text.Replace($secret, '[REDACTED]') }
    }
    return $Text
}

function Start-Child([string] $File, [string[]] $Arguments, [hashtable] $Settings = @{}, [string] $Directory = $root) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    foreach ($key in @($info.Environment.Keys)) {
        if ($key.Contains('__') -or $key -match '^(ASPNETCORE_|DOTNET_ENVIRONMENT$|MCP_|PERMISSION_SYSTEM_)') {
            $null = $info.Environment.Remove($key)
        }
    }
    foreach ($key in $Settings.Keys) { $info.Environment[$key] = [string]$Settings[$key] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $null = $process.Start()
    $child = @{ process = $process; stdout = $process.StandardOutput.ReadToEndAsync(); stderr = $process.StandardError.ReadToEndAsync() }
    $children.Add($child)
    return $child
}

function Invoke-Child([string] $File, [string[]] $Arguments, [hashtable] $Settings = @{}, [string] $Directory = $root) {
    if ($File -eq 'sqlcmd') { $Arguments = @('-I') + $Arguments }
    $child = Start-Child $File $Arguments $Settings $Directory
    if (-not $child.process.WaitForExit(180000)) {
        $child.process.Kill($true)
        throw "Child process timed out during $stage."
    }
    # LocalDB can inherit the pipe in its long-lived SQL Server child process.
    # Bound stream waits after the launcher exits instead of waiting for the database to stop.
    $output = ''
    foreach ($stream in @($child.stdout, $child.stderr)) {
        if ($stream.Wait(1000)) { $output += $stream.GetAwaiter().GetResult() }
    }
    if ($child.process.ExitCode -ne 0) {
        [IO.File]::WriteAllText((Join-Path $privateDirectory 'command-error.txt'), (Protect-Text $output))
        throw "Child process failed during $stage (exit $($child.process.ExitCode)); diagnostic: $privateDirectory/command-error.txt"
    }
    return $output
}

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}

function Invoke-Sql([string] $Sql, [string] $Database = 'master') {
    return Invoke-Child 'sqlcmd' @('-S', "(localdb)\$runId", '-E', '-C', '-b', '-h', '-1', '-W', '-d', $Database, '-Q', $Sql)
}

function Send-Http([string] $Method, [string] $Url, [object] $Body = $null, [hashtable] $Headers = @{}, [switch] $Form, [Net.Http.HttpClient] $Client = $http) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), $Url)
    try {
        foreach ($key in $Headers.Keys) { $null = $request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key]) }
        if ($null -ne $Body) {
            if ($Form) {
                $pairs = [Collections.Generic.Dictionary[string,string]]::new()
                foreach ($key in $Body.Keys) { $pairs.Add($key, [string]$Body[$key]) }
                $request.Content = [Net.Http.FormUrlEncodedContent]::new($pairs)
            } else {
                $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 30 -Compress), [Text.Encoding]::UTF8, 'application/json')
            }
        }
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $responseHeaders = @{}
            foreach ($header in $response.Headers) { $responseHeaders[$header.Key] = $header.Value -join ',' }
            return @{ status = [int]$response.StatusCode; content = $content; headers = $responseHeaders }
        } finally { $response.Dispose() }
    } catch { throw "HTTP request failed during $stage; response body omitted." }
    finally { $request.Dispose() }
}

function Invoke-Api([string] $Method, [string] $Path, [object] $Body = $null, [hashtable] $Headers = $adminHeaders) {
    $response = Send-Http $Method "$apiUrl$Path" $Body $Headers
    if ($response.status -ne 200) { throw "API $Method $Path returned HTTP $($response.status) during $stage." }
    $payload = $response.content | ConvertFrom-Json -AsHashtable
    if (-not $payload.succeeded) { throw "API $Method $Path rejected the request during $stage." }
    return $payload['data']
}

function Get-StepUp([string] $Operation, [hashtable] $Headers = $adminHeaders) {
    $challenge = Invoke-Api 'POST' '/api/security/verification/send' @{ operationCode = $Operation } $Headers
    $verified = Invoke-Api 'POST' '/api/security/verification/verify' @{ challengeId = $challenge.challengeId; password = $adminPassword } $Headers
    $secrets.Add($verified.stepUpTicket)
    $result = $Headers.Clone()
    $result['X-Step-Up-Ticket'] = $verified.stepUpTicket
    $result['Idempotency-Key'] = [Guid]::NewGuid().ToString('N')
    return $result
}

function Get-Token([string] $Scope, [string] $TenantId = '10000000-0000-0000-0000-000000000001') {
    $response = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'password'; client_id = 'permission-admin'; client_secret = $adminClientSecret; username = 'admin'; password = $adminPassword; scope = $Scope } @{ 'X-Tenant-Id' = $TenantId } -Form
    if ($response.status -ne 200) {
        $errorBody = $response.content | ConvertFrom-Json -AsHashtable
        $reason = Protect-Text ([string]$errorBody['error'] + ': ' + [string]$errorBody['error_description'])
        throw "Password token request returned HTTP $($response.status): $reason"
    }
    $token = $response.content | ConvertFrom-Json -AsHashtable
    $secrets.Add($token.access_token)
    if ($token['refresh_token']) { $secrets.Add($token['refresh_token']) }
    return $token
}

function Wait-Ready([string] $Url, [object] $Child) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 120) {
        if ($Child.process.HasExited) {
            $diagnostic = Protect-Text ($Child.stdout.GetAwaiter().GetResult() + $Child.stderr.GetAwaiter().GetResult())
            [IO.File]::WriteAllText((Join-Path $privateDirectory 'startup-error.txt'), $diagnostic)
            throw "Application exited during $stage; diagnostic: $privateDirectory/startup-error.txt"
        }
        try {
            $response = Send-Http 'GET' $Url
            if ($response.status -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    throw "Readiness timed out during $stage."
}

function Get-MigrationSources {
    $directory = Join-Path $root 'backend/PermissionSystem.Infrastructure/Data/Migrations'
    return @(Get-ChildItem $directory -Filter '*.cs' | Sort-Object Name | ForEach-Object {
        [ordered]@{ name = $_.Name; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    })
}

try {
    $null = Get-Command dotnet, sqlcmd, sqllocaldb
    $adminPassword = 'Aa1!' + (New-Secret)
    $secrets.Add($adminPassword)
    $adminClientSecret = New-Secret
    $introspectionSecret = New-Secret
    $connection = "Server=(localdb)\$runId;Database=$runId;Integrated Security=true;TrustServerCertificate=true"
    $secrets.Add($connection)
    $settings = @{
        DOTNET_ENVIRONMENT = 'Development'; ASPNETCORE_ENVIRONMENT = 'Development'
        ConnectionStrings__DefaultConnection = $connection; ConnectionStrings__HangfireConnection = $connection
        SeedData__AdminPassword = $adminPassword; SeedData__OAuthClientSecret = $adminClientSecret
        SeedData__McpIntrospectionClientSecret = $introspectionSecret
        Security__SystemConfigEncryptionKey = New-Secret
        Cache__Provider = 'Memory'; Cache__EnableRedis = 'false'
        Hangfire__Enabled = 'false'; RabbitMQ__Enabled = 'false'
        RabbitMQ__EnableConsumers = 'false'; RabbitMQ__EnableOutboxPublisher = 'false'
        Reports__SqlReportsEnabled = 'false'; LogArchive__Enabled = 'false'; OpenTelemetry__Enabled = 'false'
        Logging__LogLevel__Default = 'Warning'; Serilog__MinimumLevel__Default = 'Fatal'
        FileStorage__Provider = 'Local'; FileStorage__Local__RootPath = (Join-Path $privateDirectory 'uploads')
        Cors__AllowedOrigins__0 = 'http://127.0.0.1'; AllowedHosts = 'localhost;127.0.0.1'
        Swagger__TenantCode = 'default'
    }
    $apiProject = Join-Path $root 'backend/PermissionSystem.Api/PermissionSystem.Api.csproj'
    $infrastructureProject = Join-Path $root 'backend/PermissionSystem.Infrastructure/PermissionSystem.Infrastructure.csproj'
    if ($PrepareOnly) {
        $stage = 'Generate migration scripts without connecting to a database'
        foreach ($project in @('PermissionSystem.Api', 'PermissionSystem.McpServer', 'PermissionSystem.Worker')) {
            $null = Invoke-Child 'dotnet' @('build', (Join-Path $root "backend/$project/$project.csproj"), '--no-restore')
        }
        $common = @('--project', $infrastructureProject, '--startup-project', $apiProject, '--no-build')
        $hostArguments = @('--', '--contentRoot', $privateDirectory, '--environment', 'Development')
        # The baseline is only applied to a freshly created, empty database.
        $null = Invoke-Child 'dotnet' (@('ef', 'migrations', 'script', '0', $previous, '--output', (Join-Path $evidenceDirectory 'predecessors.sql')) + $common + $hostArguments) $settings
        $null = Invoke-Child 'dotnet' (@('ef', 'migrations', 'script', $previous, $target, '--idempotent', '--output', (Join-Path $evidenceDirectory 'usage-estimates.sql')) + $common + $hostArguments) $settings
        $null = Invoke-Child 'dotnet' (@('ef', 'migrations', 'has-pending-model-changes') + $common + $hostArguments) $settings
        $manifest = [ordered]@{
            previous = $previous; target = $target; sources = Get-MigrationSources
            scripts = @('predecessors.sql', 'usage-estimates.sql') | ForEach-Object { @{ name = $_; sha256 = (Get-FileHash (Join-Path $evidenceDirectory $_)).Hash } }
        }
        $manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidenceDirectory 'manifest.json') -Encoding utf8
        Assert-Check 'Offline migration generation and model consistency' $true
        Write-Host "Review SQL before execution: $evidenceDirectory"
    } else {
        $stage = 'Verify reviewed migration inputs'
        $reviewed = (Resolve-Path -LiteralPath $ReviewedMigrationDirectory).Path
        $manifest = Get-Content -Raw (Join-Path $reviewed 'manifest.json') | ConvertFrom-Json -AsHashtable
        Assert-Check 'Reviewed migration range matches AIC-001' ($manifest.previous -eq $previous -and $manifest.target -eq $target)
        $currentSources = Get-MigrationSources | ConvertTo-Json -Depth 5 -Compress
        Assert-Check 'Migration sources unchanged since review' ($currentSources -eq ($manifest.sources | ConvertTo-Json -Depth 5 -Compress))
        foreach ($file in @('predecessors.sql', 'usage-estimates.sql')) {
            $expected = @($manifest.scripts | Where-Object { $_.name -eq $file })
            Assert-Check "Reviewed SQL hash: $file" ($expected.Count -eq 1 -and $expected[0].sha256 -eq (Get-FileHash (Join-Path $reviewed $file)).Hash)
        }
        if (-not $RedisServerPath) { throw 'Provide the installed Redis executable path; existing Redis services are not reused.' }
        $redisExecutable = (Resolve-Path -LiteralPath $RedisServerPath).Path
        $stage = 'Create isolated SQL Server LocalDB'
        $null = Invoke-Child 'sqllocaldb' @('create', $runId, $LocalDbVersion, '-s')
        $instanceCreated = $true
        $null = Invoke-Sql "CREATE DATABASE [$runId]"
        $stage = 'Apply reviewed predecessor migrations'
        # Historical raw SQL references newly added columns in the same generated batch.
        # EF executes the reviewed migration operations separately, as application startup does.
        $null = Invoke-Child 'dotnet' @('ef', 'database', 'update', $previous, '--project', $infrastructureProject, '--startup-project', $apiProject, '--no-build', '--', '--contentRoot', $privateDirectory, '--environment', 'Development') $settings
        $count = (Invoke-Sql 'SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory' $runId).Trim()
        Assert-Check 'All 42 predecessor migrations applied' ($count -eq '42')
        $backupPath = (Join-Path $privateDirectory 'before-usage-estimates.bak').Replace("'", "''")
        $stage = 'Back up predecessor schema'
        $null = Invoke-Sql "BACKUP DATABASE [$runId] TO DISK=N'$backupPath' WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$backupPath' WITH CHECKSUM;"
        $stage = 'Apply reviewed target migration'
        $null = Invoke-Child 'sqlcmd' @('-S', "(localdb)\$runId", '-E', '-C', '-b', '-d', $runId, '-i', (Join-Path $reviewed 'usage-estimates.sql'))
        $count = (Invoke-Sql 'SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory' $runId).Trim()
        Assert-Check 'All 43 migrations applied' ($count -eq '43')
        $columns = (Invoke-Sql "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('ai_usage_log') AND name IN ('EstimatedInputTokens','EstimatedOutputTokens') AND is_nullable=1 AND TYPE_NAME(user_type_id)='int'" $runId).Trim()
        Assert-Check 'Two nullable integer estimate columns exist' ($columns -eq '2')
        $restoreName = $runId + '_restore'
        $restoreData = (Join-Path $privateDirectory 'restore.mdf').Replace("'", "''")
        $restoreLog = (Join-Path $privateDirectory 'restore_log.ldf').Replace("'", "''")
        $stage = 'Restore backup into a separate database'
        $null = Invoke-Sql "RESTORE DATABASE [$restoreName] FROM DISK=N'$backupPath' WITH MOVE N'$runId' TO N'$restoreData', MOVE N'$($runId)_log' TO N'$restoreLog';"
        $restoredCount = (Invoke-Sql 'SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory' $restoreName).Trim()
        Assert-Check 'Backup restored to predecessor schema' ($restoredCount -eq '42')
        $stage = 'Start isolated Redis and application processes'
        $redisPort = Get-FreePort
        $apiPort = Get-FreePort
        $mcpPort = Get-FreePort
        $apiUrl = "http://127.0.0.1:$apiPort"
        $mcpUrl = "http://127.0.0.1:$mcpPort/mcp"
        $redis = Start-Child $redisExecutable @('--bind', '127.0.0.1', '--port', [string]$redisPort, '--protected-mode', 'yes', '--save', '', '--appendonly', 'no', '--dir', $privateDirectory) @{} $privateDirectory
        $settings.ConnectionStrings__Redis = "127.0.0.1:$redisPort,abortConnect=false"
        $secrets.Add($settings.ConnectionStrings__Redis)
        $settings.Cache__Provider = 'Redis'; $settings.Cache__EnableRedis = 'true'
        $settings.Cache__KeyPrefix = $runId + ':'; $settings.Redis__InstanceName = $runId + ':'
        $settings.RateLimit__Enabled = 'true'; $settings.RateLimit__Provider = 'Redis'
        $settings.RateLimit__GlobalPermitLimit = '1000'
        $settings.Hangfire__Enabled = 'true'; $settings.Hangfire__DashboardEnabled = 'false'
        $settings.Hangfire__WorkerEnabled = 'true'; $settings.Hangfire__WorkerCount = '2'
        $settings.OpenIddict__Issuer = "$apiUrl/"
        $settings.Swagger__OAuthRedirectUris__0 = "$apiUrl/swagger/oauth2-redirect.html"
        $settings.ASPNETCORE_URLS = $apiUrl
        $handler = [Net.Http.HttpClientHandler]::new()
        $handler.AllowAutoRedirect = $false
        $handler.UseProxy = $false
        $http = [Net.Http.HttpClient]::new($handler)
        $http.Timeout = [TimeSpan]::FromSeconds(15)
        $api = Start-Child 'dotnet' @((Join-Path $root 'backend/PermissionSystem.Api/bin/Debug/net10.0/PermissionSystem.Api.dll'), '--contentRoot', $privateDirectory) $settings $privateDirectory
        Wait-Ready "$apiUrl/health/ready" $api
        Assert-Check 'API readiness with isolated SQL Server and Redis' $true
        $worker = Start-Child 'dotnet' @((Join-Path $root 'backend/PermissionSystem.Worker/bin/Debug/net10.0/PermissionSystem.Worker.dll'), '--contentRoot', $privateDirectory) $settings $privateDirectory
        $mcpSettings = $settings.Clone()
        $mcpSettings.ASPNETCORE_URLS = "http://127.0.0.1:$mcpPort"
        $mcpSettings.McpAuthentication__Authority = "$apiUrl/"
        $mcpSettings.McpAuthentication__ResourceUrl = $mcpUrl
        $mcpSettings.McpAuthentication__IntrospectionClientId = 'permission-system-mcp-server'
        $mcpSettings.McpAuthentication__IntrospectionClientSecret = $introspectionSecret
        $mcp = Start-Child 'dotnet' @((Join-Path $root 'backend/PermissionSystem.McpServer/bin/Debug/net10.0/PermissionSystem.McpServer.dll'), '--contentRoot', $privateDirectory) $mcpSettings $privateDirectory
        Wait-Ready "http://127.0.0.1:$mcpPort/health/ready" $mcp
        Assert-Check 'MCP readiness with isolated SQL Server and Redis' $true
        . (Join-Path $PSScriptRoot 'aic-001-http-checks.ps1')
    }
} catch {
    $failure = Protect-Text $_.Exception.Message
    Write-Host "FAIL $stage : $failure"
} finally {
    foreach ($child in $children) {
        if (-not $child.process.HasExited) { $child.process.Kill($true); $child.process.WaitForExit() }
        $child.process.Dispose()
    }
    if ($instanceCreated) { & sqllocaldb stop $runId -k | Out-Null }
    $result = [ordered]@{
        runId = $runId; startedAt = $startedAt.ToString('O'); finishedAt = [DateTimeOffset]::UtcNow.ToString('O')
        mode = $PSCmdlet.ParameterSetName; passed = ($null -eq $failure); stage = $stage; failure = $failure
        checks = @($checks.ToArray()); localDbInstance = $(if ($instanceCreated) { $runId } else { $null })
        observations = $observations
        retainedPrivateDirectory = $privateDirectory
        limitations = @('Loopback HTTP only; production TLS not validated', 'Real provider and historic secret rotation not validated', 'No third-party client certification or production SLO approval')
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidenceDirectory 'result.json') -Encoding utf8
    Write-Host "Evidence: $evidenceDirectory/result.json"
}
if ($failure) { exit 1 }
