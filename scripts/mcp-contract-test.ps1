#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $McpUrl = "http://localhost:5071/mcp",
    [string] $AccessToken = $env:MCP_ACCESS_TOKEN,
    [string] $TokenUrl = $env:MCP_TOKEN_URL,
    [string] $ClientId = $env:MCP_CLIENT_ID,
    [string] $ClientSecret = $env:MCP_CLIENT_SECRET,
    [string] $Scope = "permission-system-mcp",
    [string] $ProtocolVersion = "2025-06-18"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resourceUri = [Uri]$McpUrl
if (-not $resourceUri.IsAbsoluteUri -or $resourceUri.Scheme -notin @("http", "https")) {
    throw "McpUrl must be an absolute HTTP or HTTPS URL."
}

$resourceUrl = $resourceUri.AbsoluteUri.TrimEnd('/')
$resourcePath = $resourceUri.AbsolutePath.TrimEnd('/')
$origin = "{0}://{1}" -f $resourceUri.Scheme, $resourceUri.Authority
$metadataUrls = @(
    "$origin/.well-known/oauth-protected-resource",
    "$origin/.well-known/oauth-protected-resource$resourcePath"
)

foreach ($metadataUrl in $metadataUrls) {
    $metadata = Invoke-RestMethod -Method Get -Uri $metadataUrl
    if ($metadata.resource.TrimEnd('/') -ne $resourceUrl) {
        throw "Protected Resource Metadata at $metadataUrl returned an unexpected resource."
    }
    if ($metadata.scopes_supported -notcontains $Scope) {
        throw "Protected Resource Metadata at $metadataUrl does not advertise scope '$Scope'."
    }
    if (-not $metadata.authorization_servers -or $metadata.authorization_servers.Count -eq 0) {
        throw "Protected Resource Metadata at $metadataUrl has no authorization server."
    }
}

$unauthorized = Invoke-WebRequest -Method Post -Uri $resourceUrl `
    -SkipHttpErrorCheck `
    -Headers @{ Accept = "application/json, text/event-stream" } `
    -ContentType "application/json" `
    -Body '{"jsonrpc":"2.0","id":0,"method":"tools/list","params":{}}'
if ($unauthorized.StatusCode -ne 401) {
    throw "Unauthenticated MCP request returned $($unauthorized.StatusCode), expected 401."
}
$challenge = [string]$unauthorized.Headers["WWW-Authenticate"]
if ($challenge -notmatch 'resource_metadata="') {
    throw "MCP 401 challenge does not contain resource_metadata."
}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    if ([string]::IsNullOrWhiteSpace($TokenUrl) -or
        [string]::IsNullOrWhiteSpace($ClientId) -or
        [string]::IsNullOrWhiteSpace($ClientSecret)) {
        throw "Set MCP_ACCESS_TOKEN, or set MCP_TOKEN_URL, MCP_CLIENT_ID and MCP_CLIENT_SECRET."
    }

    $tokenResponse = Invoke-RestMethod -Method Post -Uri $TokenUrl -ContentType "application/x-www-form-urlencoded" -Body @{
        grant_type = "client_credentials"
        client_id = $ClientId
        client_secret = $ClientSecret
        scope = $Scope
    }
    $AccessToken = [string]$tokenResponse.access_token
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw "The token endpoint did not return an access token."
    }
}

$headers = @{
    Authorization = "Bearer $AccessToken"
    Accept = "application/json, text/event-stream"
    "MCP-Protocol-Version" = $ProtocolVersion
}
$script:requestId = 0

function ConvertFrom-McpResponse {
    param([Parameter(Mandatory)] $Response)

    if ([string]::IsNullOrWhiteSpace($Response.Content)) {
        return $null
    }

    $contentType = [string]$Response.Headers["Content-Type"]
    if ($contentType -like "text/event-stream*") {
        $data = $Response.Content -split "`r?`n" |
            Where-Object { $_.StartsWith("data:", [StringComparison]::Ordinal) } |
            ForEach-Object { $_.Substring(5).Trim() } |
            Where-Object { $_ -and $_ -ne "[DONE]" } |
            Select-Object -Last 1
        if (-not $data) {
            throw "MCP SSE response did not contain a JSON data event."
        }
        return $data | ConvertFrom-Json -Depth 100
    }

    return $Response.Content | ConvertFrom-Json -Depth 100
}

function Invoke-McpRequest {
    param(
        [Parameter(Mandatory)][string] $Method,
        [hashtable] $Params = @{}
    )

    $script:requestId++
    $body = @{
        jsonrpc = "2.0"
        id = $script:requestId
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 100 -Compress
    $response = Invoke-WebRequest -Method Post -Uri $resourceUrl -Headers $headers `
        -ContentType "application/json" -Body $body
    $payload = ConvertFrom-McpResponse $response
    if ($null -eq $payload) {
        throw "MCP method '$Method' returned an empty response."
    }
    if ($payload.PSObject.Properties.Name -contains "error" -and $payload.error) {
        throw "MCP method '$Method' failed: $($payload.error.code) $($payload.error.message)"
    }
    return $payload
}

function Send-McpNotification {
    param(
        [Parameter(Mandatory)][string] $Method,
        [hashtable] $Params = @{}
    )

    $body = @{
        jsonrpc = "2.0"
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 100 -Compress
    $response = Invoke-WebRequest -Method Post -Uri $resourceUrl -Headers $headers `
        -ContentType "application/json" -Body $body
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "MCP notification '$Method' returned HTTP $($response.StatusCode)."
    }
}

function Get-McpToolPayload {
    param([Parameter(Mandatory)] $ToolResult)

    if ($ToolResult.PSObject.Properties.Name -contains "structuredContent" -and $ToolResult.structuredContent) {
        $structuredContent = $ToolResult.structuredContent
        if ($structuredContent.PSObject.Properties.Name -contains "result") {
            return $structuredContent.result
        }
        return $structuredContent
    }
    $text = $ToolResult.content |
        Where-Object { $_.type -eq "text" } |
        Select-Object -First 1 -ExpandProperty text
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "MCP tool result contains neither structuredContent nor text content."
    }
    return $text | ConvertFrom-Json -Depth 100
}

$initialize = Invoke-McpRequest -Method "initialize" -Params @{
    protocolVersion = $ProtocolVersion
    capabilities = @{}
    clientInfo = @{ name = "permission-system-contract-test"; version = "1.0" }
}
if (-not $initialize.result.protocolVersion) {
    throw "MCP initialize response does not contain a protocol version."
}
Send-McpNotification -Method "notifications/initialized"

$tools = Invoke-McpRequest -Method "tools/list"
$toolNames = @($tools.result.tools | ForEach-Object { $_.name })
foreach ($requiredTool in @("list_datasets", "describe_dataset", "query_dataset")) {
    if ($toolNames -notcontains $requiredTool) {
        throw "MCP tool list does not contain '$requiredTool'."
    }
}

$listResult = Invoke-McpRequest -Method "tools/call" -Params @{
    name = "list_datasets"
    arguments = @{}
}
if ($listResult.result.PSObject.Properties.Name -contains "isError" -and $listResult.result.isError) {
    throw "list_datasets returned an MCP tool error."
}
$datasets = @(Get-McpToolPayload $listResult.result)
if ($datasets.Count -eq 0) {
    throw "The MCP client has no authorized datasets to validate."
}

$datasetCode = [string]$datasets[0].datasetCode
$describeResult = Invoke-McpRequest -Method "tools/call" -Params @{
    name = "describe_dataset"
    arguments = @{ datasetCode = $datasetCode }
}
if ($describeResult.result.PSObject.Properties.Name -contains "isError" -and $describeResult.result.isError) {
    throw "describe_dataset returned an MCP tool error."
}
$description = Get-McpToolPayload $describeResult.result
if ([string]::IsNullOrWhiteSpace([string]$description.schemaHash)) {
    throw "describe_dataset did not return a Schema Hash."
}

$queryResult = Invoke-McpRequest -Method "tools/call" -Params @{
    name = "query_dataset"
    arguments = @{ datasetCode = $datasetCode; limit = 1 }
}
if ($queryResult.result.PSObject.Properties.Name -contains "isError" -and $queryResult.result.isError) {
    throw "query_dataset returned an MCP tool error."
}
$query = Get-McpToolPayload $queryResult.result
if ($query.datasetCode -ne $datasetCode -or [string]::IsNullOrWhiteSpace([string]$query.traceId)) {
    throw "query_dataset returned an invalid contract payload."
}

Write-Host "MCP contract checks passed: metadata, challenge, initialize, tools/list and dataset tools ($datasetCode)."
