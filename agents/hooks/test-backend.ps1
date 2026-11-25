[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testProject = 'tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj'
$runDirectory = Join-Path $projectRoot "test-results/unit-contracts-$([guid]::NewGuid().ToString('N'))"
$rtkCommand = Get-Command rtk -ErrorAction SilentlyContinue

function Invoke-DotnetCheck {
    param([string[]]$Arguments)

    if ($rtkCommand) {
        & $rtkCommand.Source dotnet @Arguments
    } else {
        & dotnet @Arguments
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Push-Location -LiteralPath $projectRoot
try {
    Write-Host "Unit/contract evidence: $runDirectory"
    Invoke-DotnetCheck -Arguments @('restore', $testProject, '--locked-mode')
    Invoke-DotnetCheck -Arguments @('build', $testProject, '--configuration', $Configuration, '--no-restore')
    & dotnet run --project $testProject --configuration $Configuration --no-build --no-restore -- --scenario-evidence $runDirectory
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
