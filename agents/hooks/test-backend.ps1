#Requires -Version 7
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $PSScriptRoot 'BackendBuild.ps1')
. (Join-Path $PSScriptRoot 'BackendExport.ps1')

function Get-UtcTimestamp { [DateTimeOffset]::UtcNow.ToString('o') }

$runId = [guid]::NewGuid()
$runDirectory = Assert-RegularPath $projectRoot "test-results/unit-contracts-$($runId.ToString('N'))"
if ([IO.Path]::Exists($runDirectory)) { throw 'run-directory-exists' }
$null = [IO.Directory]::CreateDirectory($runDirectory)
$contextPath = Join-Path $runDirectory 'run.json'
$phases = [ordered]@{}
foreach ($name in 'preflight', 'restore', 'build', 'policy', 'test', 'evidence', 'export') {
    $phases[$name] = [ordered]@{ status = 'pending'; startedUtc = $null; finishedUtc = $null; exitCode = $null; failureCode = $null }
}
$manifest = [ordered]@{
    schemaVersion = 1; runId = $runId.ToString('D'); startedUtc = Get-UtcTimestamp; finishedUtc = $null
    source = $null; environment = [ordered]@{
        os = if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'Other' }
        powerShellVersion = $PSVersionTable.PSVersion.ToString()
        dotnetSdk = $null; configuration = $Configuration; execution = 'native'
    }
    phases = $phases; result = 'running'; evidence = @{ suite = 'suite'; export = 'export' }
    buildAttestation = $null; attachmentPolicy = $null
}

function Start-Phase([string]$Name) {
    $phases[$Name].status = 'running'
    $phases[$Name].startedUtc = Get-UtcTimestamp
    Write-EvidenceJson $contextPath $manifest
}

function Finish-Phase([string]$Name, [string]$Status, $Code = $null, [string]$Reason = '') {
    $phases[$Name].status = $Status
    $phases[$Name].finishedUtc = Get-UtcTimestamp
    $phases[$Name].exitCode = $Code
    $phases[$Name].failureCode = if ($Reason) { $Reason } else { $null }
}

function Assert-SameSource {
    $current = (Get-SourceSnapshot).Source
    if ($current.commit -cne $manifest.source.commit -or $current.digest -cne $manifest.source.digest) {
        throw 'source-changed'
    }
}

Write-EvidenceJson $contextPath $manifest
$exitCode = 0
$phase = 'preflight'
$safeReceipt = $null
$safeResults = $null
$artifactDirectory = $null
$stageReason = 'preflight-failed'
try {
    Start-Phase $phase
    $gitCommand = Get-Command git -CommandType Application | Select-Object -First 1
    $dotnetCommand = Get-Command dotnet -CommandType Application | Select-Object -First 1
    $rtkCommand = Get-Command rtk -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($rtkCommand) { $manifest.environment.execution = 'rtk' }
    $snapshot = Get-SourceSnapshot
    $manifest.source = $snapshot.Source
    Assert-BuildInputs $snapshot.Paths
    $sdk = Invoke-BackendProcess $dotnetCommand.Source @('--version') -Capture
    $pinnedSdk = (Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'global.json') | ConvertFrom-Json).sdk.version
    if ($sdk.ExitCode -ne 0 -or $pinnedSdk -cnotmatch '^\d+\.\d+\.\d+$' -or $sdk.Output.Trim() -cne $pinnedSdk) {
        throw 'sdk-unavailable'
    }
    $manifest.environment.dotnetSdk = $pinnedSdk
    Finish-Phase $phase passed 0

    $buildRoot = Join-Path $runDirectory 'build'
    foreach ($phase in 'restore', 'build') {
        $stageReason = "$phase-failed"
        Start-Phase $phase
        $arguments = @($phase, $testProject, '--artifacts-path', $buildRoot)
        $arguments += if ($phase -eq 'restore') { '--locked-mode' }
            else { @('--configuration', $Configuration, '--no-restore', '--no-incremental') }
        $commandResult = Invoke-BuildCommand $arguments
        if ($commandResult.ExitCode -ne 0) {
            $exitCode = $commandResult.ExitCode
            throw $stageReason
        }
        if ($phase -eq 'build') {
            Assert-SameSource
            $artifactDirectory = Assert-RegularPath $buildRoot "bin/Booking.ContractTests/$($Configuration.ToLowerInvariant())"
            $artifactDigest = Get-ArtifactDigest $artifactDirectory
            $manifest.buildAttestation = @{ sourceDigest = $manifest.source.digest; artifactDigest = $artifactDigest }
        }
        Finish-Phase $phase passed 0
    }

    $phase = 'policy'
    $stageReason = 'policy-failed'
    Start-Phase $phase
    $assemblies = @($ownedAssemblies | ForEach-Object { Join-Path $artifactDirectory $_ })
    $policyResult = Invoke-BackendProcess (Join-Path $PSHOME 'pwsh') (
        @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'check-attachment-policy.ps1')) + $assemblies) -Capture
    if ($policyResult.ExitCode -ne 0) { throw $stageReason }
    $policy = $policyResult.Output | ConvertFrom-Json -AsHashtable
    if ($policy.policyVersion -ne 1 -or $policy.outcome -cne 'passed' -or $null -ne $policy.failureCode -or
        $policy.assembliesChecked -ne $assemblies.Count -or $policy.assemblySha256.Count -ne $assemblies.Count -or
        !(Test-JsonInteger $policy.methodsChecked) -or $policy.methodsChecked -eq 0) { throw $stageReason }
    $hashes = @(foreach ($assembly in $assemblies) {
        [Convert]::ToHexString((Get-DigestBytes $assembly)).ToLowerInvariant()
    })
    for ($i = 0; $i -lt $hashes.Count; $i++) {
        if ($hashes[$i] -cne $policy.assemblySha256[$i]) { throw $stageReason }
    }
    if ((Get-ArtifactDigest $artifactDirectory) -cne $artifactDigest) { throw $stageReason }
    Assert-SameSource
    $manifest.attachmentPolicy = @{
        policyVersion = 1; outcome = 'passed'; failureCode = $null; artifactDigest = $artifactDigest
        assembliesChecked = $hashes.Count; methodsChecked = $policy.methodsChecked; assemblySha256 = $hashes
    }
    Finish-Phase $phase passed 0

    $phase = 'test'
    $stageReason = 'test-launch-failed'
    Start-Phase $phase
    $child = Invoke-BackendProcess $dotnetCommand.Source @($assemblies[0], '--scenario-evidence',
        (Join-Path $runDirectory 'suite'), '--run-context', $contextPath) -TestProcess
    $exitCode = $child.ExitCode
    $stageReason = 'test-receipt-unavailable'
    $phase = 'evidence'
    Start-Phase $phase
    $safeReceipt = Read-SafeReceipt $child.ExitCode
    if ($safeReceipt.testOutcome -ceq 'pending') { Finish-Phase test notRun }
    elseif ($safeReceipt.testOutcome -ceq 'passed') { Finish-Phase test passed 0 }
    else { Finish-Phase test failed $safeReceipt.platformExitCode 'platform-tests-failed' }
    if ($safeReceipt.evidenceOutcome -cne 'passed') { throw 'invalid-scenario-evidence' }
    $stageReason = 'invalid-scenario-evidence'
    Assert-SameSource
    if ((Get-ArtifactDigest $artifactDirectory) -cne $artifactDigest) { throw $stageReason }
    $safeResults = Read-SafeResults $safeReceipt
    Finish-Phase $phase passed 0
} catch {
    if (!$exitCode) { $exitCode = 1 }
    $codes = @('source-unavailable', 'source-changed', 'unsafe-input-path', 'linked-input-path', 'ignored-build-input',
        'unexpected-input-directory', 'unexpected-build-import', 'sdk-unavailable', 'invalid-artifacts', 'invalid-scenario-evidence')
    $reason = if ($_.Exception.Message -cin $codes) { $_.Exception.Message } else { $stageReason }
    Finish-Phase $phase failed $exitCode $reason
} finally {
    foreach ($name in 'preflight', 'restore', 'build', 'policy', 'test', 'evidence') {
        if ($phases[$name].status -ceq 'pending') { Finish-Phase $name notRun }
        if ($phases[$name].status -ceq 'running') { Finish-Phase $name failed $null 'phase-incomplete' }
    }
    $export = $null
    try {
        Start-Phase export
        $export = Write-SafeExport
        Finish-Phase export passed 0
        $manifest.result = if ($exitCode) { 'failed' } else { 'passed' }
        $manifest.finishedUtc = Get-UtcTimestamp
        Write-EvidenceJson (Join-Path $export 'run.json') $manifest
        if ($env:GITHUB_OUTPUT) { [IO.File]::AppendAllText($env:GITHUB_OUTPUT, "export_path=$export`n") }
        Write-Host "Backend evidence: $export"
    } catch {
        if (!$exitCode) { $exitCode = 1 }
        Finish-Phase export failed 1 'export-failed'
    }
    $manifest.result = if ($exitCode) { 'failed' } else { 'passed' }
    $manifest.finishedUtc = Get-UtcTimestamp
    Write-EvidenceJson $contextPath $manifest
    if ($export -and $phases.export.status -ceq 'failed') {
        try { Write-EvidenceJson (Join-Path $export 'run.json') $manifest } catch {}
    }
    foreach ($name in $phases.Keys) {
        Write-Host "$name $($phases[$name].status) $($phases[$name].failureCode)"
    }
    if ($safeResults -and $phases.evidence.status -ceq 'passed') {
        Write-Host "Passed $($safeResults.Passed), failed $($safeResults.Failed)"
    }
}
exit $exitCode
