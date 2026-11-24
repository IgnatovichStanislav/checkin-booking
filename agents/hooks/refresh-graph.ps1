[CmdletBinding()]
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'AGENTS.md'))) {
    throw 'Workspace AGENTS.md not found.'
}
$graphifyCommand = Get-Command graphify -ErrorAction Stop
if ($Check) {
    Write-Output "Graphify available: $($graphifyCommand.Source)"
    Write-Output "Workspace: $projectRoot"
    return
}
Push-Location -LiteralPath $projectRoot
try {
    & $graphifyCommand.Source extract . --code-only --no-cluster --max-workers 1
    if ($LASTEXITCODE -ne 0) { throw "Graphify extraction failed (exit $LASTEXITCODE)." }
    if (-not (Test-Path -LiteralPath 'graphify-out/graph.json')) {
        throw 'Graphify returned without creating graph.json.'
    }
} finally {
    Pop-Location
}
