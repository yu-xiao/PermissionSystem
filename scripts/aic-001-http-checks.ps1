# Invoked in the isolated environment owned by aic-001-local-acceptance.ps1.
if (-not (Get-Variable -Name runId -ErrorAction SilentlyContinue) -or $runId -notmatch '^aic001_[a-f0-9]{32}$') {
    throw 'Run aic-001-local-acceptance.ps1 to provision the isolated environment first.'
}

function Get-HiddenFields([string] $Html) {
    $fields = @{}
    foreach ($match in [regex]::Matches($Html, '<input[^>]*type="hidden"[^>]*name="([^"]+)"[^>]*value="([^"]*)"')) {
        $fields[[Net.WebUtility]::HtmlDecode($match.Groups[1].Value)] = [Net.WebUtility]::HtmlDecode($match.Groups[2].Value)
    }
    return $fields
}

function ConvertTo-Query([hashtable] $Values) {
    return (($Values.Keys | Sort-Object | ForEach-Object { [Uri]::EscapeDataString($_) + '=' + [Uri]::EscapeDataString([string]$Values[$_]) }) -join '&')
}

function Invoke-Mcp([string] $Method, [hashtable] $Parameters, [string] $Token, [hashtable] $AdditionalHeaders = @{}) {
    $headers = @{ Accept = 'application/json, text/event-stream'; 'MCP-Protocol-Version' = '2025-06-18' }
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    foreach ($key in $AdditionalHeaders.Keys) { $headers[$key] = $AdditionalHeaders[$key] }
    return Send-Http 'POST' $mcpUrl @{ jsonrpc = '2.0'; id = 1; method = $Method; params = $Parameters } $headers
}

function Read-Mcp([hashtable] $Response) {
    if ($Response.status -ne 200) { throw "MCP returned HTTP $($Response.status) during $stage." }
    $json = $Response.content
    if ($json.TrimStart().StartsWith('event:') -or $json.TrimStart().StartsWith('data:')) {
        $json = @($json -split '\r?\n' | Where-Object { $_.StartsWith('data:') } | ForEach-Object { $_.Substring(5).Trim() })[-1]
    }
    return $json | ConvertFrom-Json -AsHashtable
}

function Read-Tool([hashtable] $Response) {
    $payload = Read-Mcp $Response
    if ($payload['error'] -or $payload.result['isError']) { throw "MCP tool rejected the request during $stage." }
    if ($payload.result['structuredContent']) {
        $structured = $payload.result.structuredContent
        if ($structured.ContainsKey('result')) { return $structured.result }
        return $structured
    }
    return ($payload.result.content | Where-Object { $_.type -eq 'text' } | Select-Object -First 1).text | ConvertFrom-Json -AsHashtable
}

$stage = 'Swagger PKCE, callback and tenant validation'
$swagger = Send-Http 'GET' "$apiUrl/swagger/index.html"
Assert-Check 'Swagger UI is available' ($swagger.status -eq 200)
Assert-Check 'Swagger UI does not expose the seeded client secret' (-not $swagger.content.Contains($adminClientSecret))
$verifier = New-Secret
$challengeBytes = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::ASCII.GetBytes($verifier))
$challenge = [Convert]::ToBase64String($challengeBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$oauthParameters = @{
    client_id = 'permission-swagger'; response_type = 'code'; redirect_uri = "$apiUrl/swagger/oauth2-redirect.html"
    scope = 'permission-system-api offline_access'; state = [Guid]::NewGuid().ToString('N')
    code_challenge = $challenge; code_challenge_method = 'S256'; tenant = 'default'
}
$invalidCallback = $oauthParameters.Clone()
$invalidCallback.redirect_uri = "$apiUrl/unregistered-callback"
$rejected = Send-Http 'GET' ("$apiUrl/connect/authorize?" + (ConvertTo-Query $invalidCallback))
Assert-Check 'Unregistered OAuth callback is rejected' ($rejected.status -eq 400)
$invalidTenant = $oauthParameters.Clone()
$invalidTenant.tenant = 'aic001-does-not-exist'
$rejected = Send-Http 'GET' ("$apiUrl/connect/authorize?" + (ConvertTo-Query $invalidTenant))
$observations.unknownTenantStatus = $rejected.status
Assert-Check 'Unknown OAuth tenant is rejected' ($rejected.status -eq 400 -or ($rejected.status -eq 302 -and $rejected.headers['Location'] -like "$apiUrl/swagger/oauth2-redirect.html?*" -and $rejected.headers['Location'] -match '[?&]error=invalid_request(&|$)'))
$missingPkce = $oauthParameters.Clone()
$missingPkce.Remove('code_challenge'); $missingPkce.Remove('code_challenge_method')
$rejected = Send-Http 'GET' ("$apiUrl/connect/authorize?" + (ConvertTo-Query $missingPkce))
Assert-Check 'Public OAuth client requires PKCE' ($rejected.status -eq 400 -or ($rejected.status -eq 302 -and $rejected.headers.Location -match 'error='))
$login = Send-Http 'GET' ("$apiUrl/connect/authorize?" + (ConvertTo-Query $oauthParameters))
Assert-Check 'OAuth login form contains antiforgery and authorization ticket' ($login.status -eq 200 -and $login.content.Contains('__RequestVerificationToken') -and $login.content.Contains('authorization_ticket'))
$form = Get-HiddenFields $login.content
$form.username = 'admin'; $form.password = $adminPassword; $form.decision = 'login'
$consentRedirect = Send-Http 'POST' "$apiUrl/connect/authorize" $form @{} -Form
Assert-Check 'OAuth login proceeds to consent' ($consentRedirect.status -eq 302)
$consentUrl = [Uri]::new([Uri]$apiUrl, [string]$consentRedirect.headers.Location).AbsoluteUri
$consent = Send-Http 'GET' $consentUrl
$form = Get-HiddenFields $consent.content
$form.decision = 'approve'
$callback = Send-Http 'POST' "$apiUrl/connect/authorize" $form @{} -Form
$callbackUri = [Uri]$callback.headers.Location
$callbackQuery = [Web.HttpUtility]::ParseQueryString($callbackUri.Query)
Assert-Check 'OAuth callback and state match exactly' ($callback.status -eq 302 -and $callbackUri.GetLeftPart([UriPartial]::Path) -eq $oauthParameters.redirect_uri -and $callbackQuery['state'] -eq $oauthParameters.state -and $callbackQuery['code'])
$secrets.Add($callbackQuery['code'])
$exchange = @{ grant_type = 'authorization_code'; client_id = 'permission-swagger'; code = $callbackQuery['code']; code_verifier = $verifier; redirect_uri = $oauthParameters.redirect_uri }
$exchanged = Send-Http 'POST' "$apiUrl/connect/token" $exchange @{} -Form
Assert-Check 'Public Swagger client exchanges authorization code without a secret' ($exchanged.status -eq 200)
$swaggerToken = $exchanged.content | ConvertFrom-Json -AsHashtable
$secrets.Add($swaggerToken.access_token); $secrets.Add($swaggerToken.refresh_token)
$replayed = Send-Http 'POST' "$apiUrl/connect/token" $exchange @{} -Form
Assert-Check 'Authorization code replay is rejected' ($replayed.status -eq 400)

# External MCP clients use bearer credentials independently of the Swagger browser session.
$http.Dispose()
$handler.Dispose()
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseProxy = $false
$handler.UseCookies = $false
$http = [Net.Http.HttpClient]::new($handler)
$http.Timeout = [TimeSpan]::FromSeconds(15)

$stage = 'MCP discovery, token introspection and delegated identity'
$adminToken = Get-Token 'openid profile permission-system-api offline_access'
$adminHeaders = @{ Authorization = 'Bearer ' + $adminToken.access_token }
$offlineRequest = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'password'; client_id = 'permission-admin'; client_secret = $adminClientSecret; username = 'admin'; password = $adminPassword; scope = 'permission-system-mcp offline_access' } @{ 'X-Tenant-Id' = '10000000-0000-0000-0000-000000000001' } -Form
Assert-Check 'MCP delegated offline access is rejected' ($offlineRequest.status -eq 400 -and ($offlineRequest.content | ConvertFrom-Json -AsHashtable).error -eq 'invalid_scope')
$delegatedToken = Get-Token 'openid profile permission-system-api permission-system-mcp'
Assert-Check 'Delegated MCP token has no refresh token' (-not $delegatedToken.ContainsKey('refresh_token'))
$introspection = Send-Http 'POST' "$apiUrl/connect/introspect" @{ token = $delegatedToken.access_token; client_id = 'permission-system-mcp-server'; client_secret = $introspectionSecret } @{} -Form
$identity = $introspection.content | ConvertFrom-Json -AsHashtable
$observations.introspection = @{ status = $introspection.status; active = $identity['active']; error = $identity['error'] }
$probe = Invoke-Mcp 'tools/list' @{} $delegatedToken.access_token
$observations.delegatedMcpStatus = $probe.status
Assert-Check 'Delegated token is active through real introspection' ($introspection.status -eq 200 -and $identity['active'])
$unauthorized = Invoke-Mcp 'tools/list' @{} ''
Assert-Check 'Anonymous MCP request returns 401 and discovery challenge' ($unauthorized.status -eq 401 -and $unauthorized.headers['WWW-Authenticate'] -match 'resource_metadata=')
$allowed = Invoke-Mcp 'tools/list' @{} $delegatedToken.access_token
$tools = Read-Mcp $allowed
Assert-Check 'Delegated token reaches external MCP through introspection' ($tools.result.tools.Count -gt 0)
$wrongAudience = Invoke-Mcp 'tools/list' @{} $adminToken.access_token
Assert-Check 'API-only token cannot access MCP' ($wrongAudience.status -in @(401,403))
$crossTenant = Invoke-Mcp 'tools/list' @{} $delegatedToken.access_token @{ 'X-Tenant-Id' = [Guid]::NewGuid().ToString() }
Assert-Check 'Delegated tenant header override is rejected' ($crossTenant.status -in @(401,403))

$stage = 'Create authorized service client with real step-up verification'
$datasets = @(Invoke-Api 'GET' '/api/ai/mcp/datasets')
$department = @($datasets | Where-Object { $_.datasetCode -eq 'department-directory' })[0]
$createRequest = @{
    clientCode = $runId; clientName = 'AIC-001 isolated acceptance'; allowedScopes = @('mcp:dataset:list','mcp:dataset:describe','mcp:dataset:query')
    allowedIpList = '127.0.0.1'; rateLimitPerMinute = 1000
    datasetGrants = @(@{ datasetId = $department.id; allowedFields = @('code','name') })
}
$noStepUp = Send-Http 'POST' "$apiUrl/api/ai/mcp/clients" $createRequest $adminHeaders
Assert-Check 'MCP client creation without step-up is rejected' ($noStepUp.status -eq 403)
$credential = Invoke-Api 'POST' '/api/ai/mcp/clients' $createRequest (Get-StepUp 'ai:mcp-client:create')
$secrets.Add($credential.clientSecret)
$serviceTokenResponse = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'client_credentials'; client_id = $credential.client.oAuthClientId; client_secret = $credential.clientSecret; scope = 'permission-system-mcp' } @{} -Form
Assert-Check 'Bound MCP service client can obtain a token' ($serviceTokenResponse.status -eq 200)
$serviceToken = $serviceTokenResponse.content | ConvertFrom-Json -AsHashtable
$secrets.Add($serviceToken.access_token)
$serviceTools = Read-Mcp (Invoke-Mcp 'tools/list' @{} $serviceToken.access_token)
$names = @($serviceTools.result.tools | ForEach-Object { $_.name } | Sort-Object)
Assert-Check 'Service client exposes only three dataset tools' (($names -join ',') -eq 'describe_dataset,list_datasets,query_dataset')
$crossTenant = Invoke-Mcp 'tools/list' @{} $serviceToken.access_token @{ 'X-Tenant-Id' = [Guid]::NewGuid().ToString() }
Assert-Check 'Service tenant header override is rejected' ($crossTenant.status -in @(401,403))
$query = Read-Tool (Invoke-Mcp 'tools/call' @{ name = 'query_dataset'; arguments = @{ datasetCode = 'department-directory'; limit = 1 } } $serviceToken.access_token)
Assert-Check 'Dataset query honors approved fields and emits trace ID' ($query.traceId -and (($query.fields | Sort-Object) -join ',') -eq 'code,name' -and $query.rowCount -le 1)
$deniedFields = Read-Mcp (Invoke-Mcp 'tools/call' @{ name = 'query_dataset'; arguments = @{ datasetCode = 'department-directory'; fields = @('isEnabled'); limit = 1 } } $serviceToken.access_token)
Assert-Check 'Unapproved dataset field is rejected' ([bool]($deniedFields['error'] -or $deniedFields.result['isError']))
$deniedDataset = Read-Mcp (Invoke-Mcp 'tools/call' @{ name = 'query_dataset'; arguments = @{ datasetCode = 'platform-capabilities'; limit = 1 } } $serviceToken.access_token)
Assert-Check 'Ungranted dataset is rejected' ([bool]($deniedDataset['error'] -or $deniedDataset.result['isError']))
$stage = 'Existing MCP HTTP contract suite'
& (Join-Path $PSScriptRoot 'mcp-contract-test.ps1') -McpUrl $mcpUrl -AccessToken $serviceToken.access_token
Assert-Check 'Existing metadata, initialize and dataset contract suite' $true

$stage = 'Local load baseline; not a production SLO'
$loadOutput = & (Join-Path $PSScriptRoot 'mcp-load-test.ps1') -McpUrl $mcpUrl -AccessToken $serviceToken.access_token -DatasetCode 'department-directory' -Concurrency 5 -TotalRequests 50 -MaxErrorRatePercent 0 -MaxP95Milliseconds 60000 6>&1
$observations.loadBaseline = ($loadOutput | Out-String).Trim()
Write-Host $observations.loadBaseline
Assert-Check '50-request local load smoke test has zero failures' $true

$stage = 'Distributed rate limit across two real MCP processes'
$secondPort = Get-FreePort
$secondUrl = "http://127.0.0.1:$secondPort/mcp"
$secondSettings = $mcpSettings.Clone()
$secondSettings.ASPNETCORE_URLS = "http://127.0.0.1:$secondPort"
$secondSettings.McpAuthentication__ResourceUrl = $secondUrl
$secondMcp = Start-Child 'dotnet' @((Join-Path $root 'backend/PermissionSystem.McpServer/bin/Debug/net10.0/PermissionSystem.McpServer.dll'), '--contentRoot', $privateDirectory) $secondSettings $privateDirectory
Wait-Ready "http://127.0.0.1:$secondPort/health/ready" $secondMcp
$limitedRequest = $createRequest.Clone()
$limitedRequest.clientCode = $runId + '_limited'
$limitedRequest.rateLimitPerMinute = 3
$limitedCredential = Invoke-Api 'POST' '/api/ai/mcp/clients' $limitedRequest (Get-StepUp 'ai:mcp-client:create')
$secrets.Add($limitedCredential.clientSecret)
$limitedResponse = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'client_credentials'; client_id = $limitedCredential.client.oAuthClientId; client_secret = $limitedCredential.clientSecret; scope = 'permission-system-mcp' } @{} -Form
Assert-Check 'Rate-limit test client obtains a token' ($limitedResponse.status -eq 200)
$limitedToken = ($limitedResponse.content | ConvertFrom-Json -AsHashtable).access_token
$secrets.Add($limitedToken)
while ([DateTimeOffset]::UtcNow.Second -gt 50) { Start-Sleep -Milliseconds 250 }
$pending = @()
try {
    for ($index = 0; $index -lt 12; $index++) {
        $url = if ($index % 2 -eq 0) { $mcpUrl } else { $secondUrl }
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, $url)
        $request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $limitedToken)
        $null = $request.Headers.TryAddWithoutValidation('Accept', 'application/json, text/event-stream')
        $null = $request.Headers.TryAddWithoutValidation('MCP-Protocol-Version', '2025-06-18')
        $request.Content = [Net.Http.StringContent]::new('{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}', [Text.Encoding]::UTF8, 'application/json')
        $pending += @{ request = $request; task = $http.SendAsync($request) }
    }
    $statuses = @()
    $retryAfterCount = 0
    foreach ($item in $pending) {
        $response = $item.task.GetAwaiter().GetResult()
        try {
            $statuses += [int]$response.StatusCode
            if ([int]$response.StatusCode -eq 429 -and $response.Headers.RetryAfter.Delta.TotalSeconds -gt 0) { $retryAfterCount++ }
        } finally { $response.Dispose() }
    }
    $observations.distributedRateLimit = @{ requests = 12; processes = 2; permits = 3; accepted = @($statuses | Where-Object { $_ -eq 200 }).Count; rateLimited = @($statuses | Where-Object { $_ -eq 429 }).Count; retryAfter = $retryAfterCount }
    Assert-Check 'Redis shares exactly three permits across two MCP processes' ($observations.distributedRateLimit.accepted -eq 3 -and $observations.distributedRateLimit.rateLimited -eq 9 -and $retryAfterCount -eq 9)
} finally {
    foreach ($item in $pending) { $item.request.Dispose() }
}

$stage = 'Isolated service-client secret rotation'
$client = Invoke-Api 'GET' "/api/ai/mcp/clients/$($credential.client.id)"
$rotated = Invoke-Api 'POST' "/api/ai/mcp/clients/$($client.id)/rotate-secret" @{ concurrencyToken = $client.concurrencyToken } (Get-StepUp 'ai:mcp-client:secret')
$secrets.Add($rotated.clientSecret)
$oldSecretResponse = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'client_credentials'; client_id = $client.oAuthClientId; client_secret = $credential.clientSecret; scope = 'permission-system-mcp' } @{} -Form
Assert-Check 'Old isolated service secret cannot mint a token after rotation' ($oldSecretResponse.status -in @(400,401))
$newSecretResponse = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'client_credentials'; client_id = $client.oAuthClientId; client_secret = $rotated.clientSecret; scope = 'permission-system-mcp' } @{} -Form
Assert-Check 'New isolated service secret can mint a token after rotation' ($newSecretResponse.status -eq 200)
$secrets.Add(($newSecretResponse.content | ConvertFrom-Json -AsHashtable).access_token)

$stage = 'Session revocation across API and MCP processes'
$sessions = Invoke-Api 'GET' '/api/online-users?PageSize=100'
$delegatedSession = @($sessions.items | Where-Object { $_.sessionId -eq $identity.session_id })
Assert-Check 'Delegated token maps to a stored user session' ($delegatedSession.Count -eq 1)
$null = Invoke-Api 'POST' "/api/online-users/$($delegatedSession[0].id)/kickout" @{ reason = 'AIC-001 isolated acceptance' }
$revoked = Invoke-Mcp 'tools/list' @{} $delegatedToken.access_token
Assert-Check 'Revoked delegated session is rejected by MCP' ($revoked.status -eq 401)

$stage = 'Tenant initialization and disabled-tenant rejection'
$tenantCode = 'aic001-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$tenant = Invoke-Api 'POST' '/api/tenants' @{ code = $tenantCode; name = 'AIC-001 isolated tenant'; administratorUserName = 'admin'; administratorDisplayName = 'Acceptance administrator'; administratorPassword = $adminPassword }
$timer = [Diagnostics.Stopwatch]::StartNew()
do {
    if ($worker.process.HasExited) { throw 'Worker exited before tenant initialization completed.' }
    $tenants = Invoke-Api 'GET' "/api/tenants?Keyword=$tenantCode"
    $tenant = @($tenants.items | Where-Object { $_.code -eq $tenantCode })[0]
    if ($tenant.status -eq 'Active' -or $tenant.status -eq 1) { break }
    Start-Sleep -Milliseconds 500
} while ($timer.Elapsed.TotalSeconds -lt 90)
Assert-Check 'Worker initialized the isolated tenant' ($tenant.status -eq 'Active' -or $tenant.status -eq 1)
$tenantToken = Get-Token 'openid profile permission-system-api permission-system-mcp' $tenant.id
$beforeDisable = Invoke-Mcp 'tools/list' @{} $tenantToken.access_token
Assert-Check 'Active second tenant can reach MCP' ($beforeDisable.status -eq 200)
$null = Invoke-Api 'POST' "/api/tenants/$($tenant.id)/disable"
$afterDisable = Invoke-Mcp 'tools/list' @{} $tenantToken.access_token
Assert-Check 'Disabled tenant cannot reach MCP' ($afterDisable.status -in @(401,403))
$failedLogin = Send-Http 'POST' "$apiUrl/connect/token" @{ grant_type = 'password'; client_id = 'permission-admin'; client_secret = $adminClientSecret; username = 'admin'; password = $adminPassword; scope = 'permission-system-mcp' } @{ 'X-Tenant-Id' = $tenant.id } -Form
Assert-Check 'Disabled tenant cannot obtain a new token' ($failedLogin.status -in @(400,403))
$stage = 'Local acceptance complete'
