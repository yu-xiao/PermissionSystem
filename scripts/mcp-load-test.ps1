#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $McpUrl = "http://localhost:5071/mcp",
    [string] $AccessToken = $env:MCP_ACCESS_TOKEN,
    [Parameter(Mandatory)][string] $DatasetCode,
    [ValidateRange(1, 200)][int] $Limit = 10,
    [ValidateRange(1, 100)][int] $Concurrency = 10,
    [ValidateRange(1, 10000)][int] $TotalRequests = 100,
    [ValidateRange(0, 100)][double] $MaxErrorRatePercent = 1,
    [ValidateRange(1, 60000)][int] $MaxP95Milliseconds = 2000,
    [string] $ProtocolVersion = "2025-06-18"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resourceUri = [Uri]$McpUrl
if (-not $resourceUri.IsAbsoluteUri -or $resourceUri.Scheme -notin @("http", "https")) {
    throw "McpUrl must be an absolute HTTP or HTTPS URL."
}
if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw "Set MCP_ACCESS_TOKEN or pass AccessToken."
}

$resourceUrl = $resourceUri.AbsoluteUri.TrimEnd('/')
$requestBody = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "query_dataset"
        arguments = @{ datasetCode = $DatasetCode; limit = $Limit }
    }
} | ConvertTo-Json -Depth 20 -Compress

$results = 1..$TotalRequests | ForEach-Object -Parallel {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest -Method Post -Uri $using:resourceUrl `
            -Headers @{
                Authorization = "Bearer $using:AccessToken"
                Accept = "application/json, text/event-stream"
                "MCP-Protocol-Version" = $using:ProtocolVersion
            } `
            -ContentType "application/json" `
            -Body $using:requestBody `
            -SkipHttpErrorCheck
        $stopwatch.Stop()
        $responseJson = $response.Content
        $contentType = [string]$response.Headers["Content-Type"]
        if ($contentType -like "text/event-stream*") {
            $responseJson = $response.Content -split "`r?`n" |
                Where-Object { $_.StartsWith("data:", [StringComparison]::Ordinal) } |
                ForEach-Object { $_.Substring(5).Trim() } |
                Where-Object { $_ -and $_ -ne "[DONE]" } |
                Select-Object -Last 1
        }
        $payload = if ($responseJson) { $responseJson | ConvertFrom-Json -Depth 100 } else { $null }
        $isProtocolError = $response.StatusCode -ge 400 -or
            $null -eq $payload -or
            ($payload.PSObject.Properties.Name -contains "error" -and $payload.error) -or
            ($payload.PSObject.Properties.Name -contains "result" -and
                $payload.result.PSObject.Properties.Name -contains "isError" -and
                $payload.result.isError)
        [pscustomobject]@{
            DurationMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            Succeeded = -not $isProtocolError
            StatusCode = $response.StatusCode
        }
    } catch {
        $stopwatch.Stop()
        [pscustomobject]@{
            DurationMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            Succeeded = $false
            StatusCode = 0
        }
    }
} -ThrottleLimit $Concurrency

$durations = @($results.DurationMilliseconds | Sort-Object)
$failed = @($results | Where-Object { -not $_.Succeeded }).Count
$errorRate = if ($results.Count -eq 0) { 100 } else { $failed * 100.0 / $results.Count }
function Get-Percentile {
    param([double[]] $Values, [double] $Percentile)
    if ($Values.Count -eq 0) { return 0 }
    $index = [Math]::Ceiling($Percentile * $Values.Count) - 1
    return $Values[[Math]::Max(0, $index)]
}

$p50 = Get-Percentile $durations 0.50
$p95 = Get-Percentile $durations 0.95
$p99 = Get-Percentile $durations 0.99
Write-Host ("MCP load result: requests={0}, concurrency={1}, failed={2}, errorRate={3:N2}%, P50={4:N0}ms, P95={5:N0}ms, P99={6:N0}ms" -f `
    $results.Count, $Concurrency, $failed, $errorRate, $p50, $p95, $p99)

if ($errorRate -gt $MaxErrorRatePercent) {
    throw "MCP error rate $($errorRate.ToString('N2'))% exceeds $MaxErrorRatePercent%."
}
if ($p95 -gt $MaxP95Milliseconds) {
    throw "MCP P95 $($p95.ToString('N0'))ms exceeds $MaxP95Milliseconds ms."
}
