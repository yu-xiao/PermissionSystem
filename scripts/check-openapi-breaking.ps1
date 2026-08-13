[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Baseline,

    [Parameter(Mandatory = $true)]
    [string] $Current
)

$ErrorActionPreference = "Stop"

function Get-OpenApiDocument([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "OpenAPI document not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json
}

function Get-Operations($Document) {
    $keys = @{}
    foreach ($pathProperty in $Document.paths.PSObject.Properties) {
        foreach ($methodProperty in $pathProperty.Value.PSObject.Properties) {
            if ($methodProperty.Name -in @("get", "post", "put", "patch", "delete", "head", "options", "trace")) {
                $keys["$($pathProperty.Name) $($methodProperty.Name.ToUpperInvariant())"] = $methodProperty.Value
            }
        }
    }
    return $keys
}

function Get-ParameterMap($Operation) {
    $parameters = @{}
    foreach ($parameter in @($Operation.parameters)) {
        if ($parameter.name -and $parameter.in) {
            $parameters["$($parameter.in) $($parameter.name)"] = $parameter
        }
    }
    return $parameters
}

function Get-ResponseMap($Operation) {
    $responses = @{}
    foreach ($response in $Operation.responses.PSObject.Properties) {
        $responses[$response.Name] = $true
    }
    return $responses
}

$baselineDocument = Get-OpenApiDocument $Baseline
$currentDocument = Get-OpenApiDocument $Current
$baselineOperations = Get-Operations $baselineDocument
$currentOperations = Get-Operations $currentDocument
$breakingChanges = [System.Collections.Generic.List[string]]::new()

foreach ($operationKey in $baselineOperations.Keys) {
    if (-not $currentOperations.ContainsKey($operationKey)) {
        $breakingChanges.Add("Removed operation: $operationKey")
        continue
    }

    $baselineOperation = $baselineOperations[$operationKey]
    $currentOperation = $currentOperations[$operationKey]
    $baselineParameters = Get-ParameterMap $baselineOperation
    $currentParameters = Get-ParameterMap $currentOperation

    foreach ($parameterKey in $baselineParameters.Keys) {
        if (-not $currentParameters.ContainsKey($parameterKey)) {
            $breakingChanges.Add("Removed parameter: $operationKey [$parameterKey]")
            continue
        }

        if (-not [bool]$baselineParameters[$parameterKey].required -and
            [bool]$currentParameters[$parameterKey].required) {
            $breakingChanges.Add("Parameter became required: $operationKey [$parameterKey]")
        }
    }

    if ($baselineOperation.requestBody -and $currentOperation.requestBody -and
        (-not [bool]$baselineOperation.requestBody.required) -and
        [bool]$currentOperation.requestBody.required) {
        $breakingChanges.Add("Request body became required: $operationKey")
    }

    if ($baselineOperation.requestBody -and $baselineOperation.requestBody.required -and
        (-not $currentOperation.requestBody -or -not $currentOperation.requestBody.required)) {
        $breakingChanges.Add("Required request body was removed: $operationKey")
    }

    $baselineResponses = Get-ResponseMap $baselineOperation
    $currentResponses = Get-ResponseMap $currentOperation
    foreach ($statusCode in $baselineResponses.Keys) {
        if (-not $currentResponses.ContainsKey($statusCode)) {
            $breakingChanges.Add("Removed response: $operationKey [$statusCode]")
        }
    }
}

if ($breakingChanges.Count -gt 0) {
    $breakingChanges | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "OpenAPI compatibility check passed. Baseline operations: $($baselineOperations.Count); current operations: $($currentOperations.Count)."
