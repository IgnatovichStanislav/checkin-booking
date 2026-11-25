#requires -Version 7.2
[CmdletBinding()]
param([switch]$Open)

$ErrorActionPreference = 'Stop'
$base = 'http://localhost:3300'
$project = "$base/project/checkin-agent-quality"

function Test-LangfuseHealth {
    try {
        $health = Invoke-RestMethod "$base/api/public/health" -TimeoutSec 5
        return $health.status -eq 'OK'
    } catch {
        return $false
    }
}

if (!(Test-LangfuseHealth)) {
    & (Join-Path $PSScriptRoot 'stack.ps1') start
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (!(Test-LangfuseHealth)) { throw "Langfuse health check failed at $base." }
}

$analytics = "$project/scores/analytics"
Write-Output "Dashboard: $project/dashboards"
Write-Output "Score analytics: $analytics"
if ($Open) { Start-Process $analytics }
