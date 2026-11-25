#Requires -Version 7
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workspace = Join-Path $repoRoot "test-results/runner-check-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $workspace
. (Join-Path $PSScriptRoot 'BackendBuild.ps1')
. (Join-Path $PSScriptRoot 'BackendExport.ps1')

function Assert-Check([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "Runner check failed: $Name." }
}

Push-Location $repoRoot
try {
    $files = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard -- @gitScope)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate runner inputs.' }
    foreach ($relative in @('.gitignore') + ($files | Select-Object -Unique)) {
        if ($relative -match $generatedPath) { continue }
        $file = Assert-RegularPath $repoRoot $relative
        if (![IO.File]::Exists($file)) { continue }
        $destination = Join-Path $workspace $relative
        $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
        [IO.File]::Copy($file, $destination)
    }
} finally { Pop-Location }

$projectRoot = $workspace
$gitCommand = Get-Command git -CommandType Application | Select-Object -First 1
$git = $gitCommand.Source
$shell = Join-Path $PSHOME 'pwsh'
$runner = Join-Path $workspace 'agents/hooks/test-backend.ps1'

function Invoke-CheckedGit([string[]]$GitArgs) {
    $result = Invoke-BackendProcess $git $GitArgs
    Assert-Check ($result.ExitCode -eq 0) 'fixture Git setup'
}

function Invoke-RunnerCase([string]$Name, [int]$ExpectedExit) {
    $reports = Join-Path $workspace 'test-results'
    $before = if (Test-Path -LiteralPath $reports) { @(Get-ChildItem -LiteralPath $reports -Directory).Name }
    $previousOutput = $env:GITHUB_OUTPUT
    try {
        $env:GITHUB_OUTPUT = $null
        $run = Invoke-BackendProcess $shell @('-NoProfile', '-File', $runner) -Capture
    } finally { $env:GITHUB_OUTPUT = $previousOutput }
    $new = @(Get-ChildItem -LiteralPath $reports -Directory | Where-Object Name -NotIn $before)
    Assert-Check ($run.ExitCode -eq $ExpectedExit -and $new.Count -eq 1) $Name
    $script:caseRoot = $new[0].FullName
    $script:caseOutput = $run.Output
    $script:caseManifest = Read-EvidenceJson $caseRoot 'run.json'
    $reexport = Read-EvidenceJson $caseRoot 'export/run.json'
    Assert-Check ($caseManifest.runId -ceq $reexport.runId -and $caseManifest.result -ceq $reexport.result) "$Name export binding"
    Write-Host "$Name passed"
}

Invoke-CheckedGit @('init', '--quiet')
Invoke-CheckedGit @('add', '--all')
Invoke-CheckedGit @('-c', 'user.name=Verification Fixture', '-c', 'user.email=fixture@example.invalid',
    '-c', 'commit.gpgsign=false', '-c', 'core.hooksPath=.git/no-hooks', 'commit', '--quiet', '-m', 'Record disposable verification inputs')

$baseline = (Get-SourceSnapshot).Source.digest
$probe = Join-Path $workspace 'src/Booking.Domain/RunnerSample.cs'
[IO.File]::WriteAllText($probe, 'namespace RunnerSample;')
Assert-Check ((Get-SourceSnapshot).Source.digest -cne $baseline) 'untracked source changes digest'
[IO.File]::Delete($probe)
Assert-Check ((Get-SourceSnapshot).Source.digest -ceq $baseline) 'untracked source restoration'
$nestedPath = Join-Path $workspace 'src/Booking.Domain/test-results'
$null = [IO.Directory]::CreateDirectory($nestedPath)
$nestedSource = Join-Path $nestedPath 'NestedSource.cs'
try {
    [IO.File]::WriteAllText($nestedSource, 'namespace NestedSource;')
    $rejected = $false
    try { $nestedDigest = (Get-SourceSnapshot).Source.digest }
    catch {
        if ($_.Exception.Message -cne 'ignored-build-input') { throw }
        $rejected = $true
    }
    Assert-Check ($rejected -or $nestedDigest -cne $baseline) 'nested build inputs cannot escape source binding'
} finally { [IO.File]::Delete($nestedSource) }
$nestedFixtureDirectory = Join-Path $workspace 'e2e/fixtures/001-booking-contracts/intake/test-results'
$null = [IO.Directory]::CreateDirectory($nestedFixtureDirectory)
$nestedFixture = Join-Path $nestedFixtureDirectory 'NestedFixture.json'
try {
    [IO.File]::WriteAllText($nestedFixture, '{}')
    $rejected = $false
    try { $nestedDigest = (Get-SourceSnapshot).Source.digest }
    catch {
        if ($_.Exception.Message -cne 'ignored-build-input') { throw }
        $rejected = $true
    }
    Assert-Check ($rejected -or $nestedDigest -cne $baseline) 'recursive intake fixtures cannot escape source binding'
} finally { [IO.File]::Delete($nestedFixture) }
$contract = Get-ChildItem -LiteralPath (Join-Path $workspace 'specs/archive/001-booking-contracts/contracts') -Filter '*.json' | Select-Object -First 1
$bytes = [IO.File]::ReadAllBytes($contract.FullName)
try {
    [IO.File]::Delete($contract.FullName)
    Assert-Check ((Get-SourceSnapshot).Source.digest -cne $baseline) 'tracked deletion changes digest'
    [IO.File]::WriteAllBytes($contract.FullName, $bytes + [byte]32)
    Assert-Check ((Get-SourceSnapshot).Source.digest -cne $baseline) 'contract mutation changes digest'
} finally { [IO.File]::WriteAllBytes($contract.FullName, $bytes) }
Assert-Check ((Get-SourceSnapshot).Source.digest -ceq $baseline) 'tracked input restoration'
Write-Host 'Source digest checks passed'

$sdkLock = Join-Path $workspace 'global.json'
$sdkLockSource = [IO.File]::ReadAllText($sdkLock)
try {
    [IO.File]::WriteAllText($sdkLock, '{"sdk":{"version":"10.0.999","rollForward":"disable"}}')
    Invoke-RunnerCase 'Unavailable pinned SDK' 1
    Assert-Check ($caseManifest.phases.preflight.failureCode -ceq 'sdk-unavailable' -and
        $caseManifest.phases.restore.status -ceq 'notRun') 'failed preflight skips restore'
} finally { [IO.File]::WriteAllText($sdkLock, $sdkLockSource) }

$runnerSource = [IO.File]::ReadAllText($runner)
try {
    $nativeRunner = [regex]::Replace($runnerSource, '(?m)^    \$rtkCommand = .+$', '    $$rtkCommand = $$null')
    Assert-Check ($nativeRunner -cne $runnerSource) 'native fixture setup'
    [IO.File]::WriteAllText($runner, $nativeRunner)
    Invoke-RunnerCase 'Native runner' 0
    Assert-Check ($caseManifest.environment.execution -ceq 'native' -and
        $caseManifest.phases.evidence.status -ceq 'passed') 'native evidence'
} finally { [IO.File]::WriteAllText($runner, $runnerSource) }

$buildHelper = Join-Path $workspace 'agents/hooks/BackendBuild.ps1'
$buildHelperSource = [IO.File]::ReadAllText($buildHelper)
try {
    [IO.File]::AppendAllText($buildHelper, @'

function Invoke-BuildCommand {
    param([string[]]$Arguments)
    $code = switch ($env:BOOKING_FIXTURE_FAIL_PHASE) {
        'restore' { 37 }
        'build' { if ($Arguments[0] -ceq 'build') { 38 } else { 0 } }
    }
    return @{ ExitCode = $code }
}
'@)
    $priorPhase = $env:BOOKING_FIXTURE_FAIL_PHASE
    try {
        $env:BOOKING_FIXTURE_FAIL_PHASE = 'restore'
        Invoke-RunnerCase 'Restore failure preserves exit' 37
        Assert-Check ($caseManifest.phases.restore.exitCode -eq 37 -and $caseManifest.phases.build.status -ceq 'notRun') 'restore stop'
        $env:BOOKING_FIXTURE_FAIL_PHASE = 'build'
        Invoke-RunnerCase 'Build failure preserves exit' 38
        Assert-Check ($caseManifest.phases.build.exitCode -eq 38 -and $caseManifest.phases.policy.status -ceq 'notRun') 'build stop'
    } finally { $env:BOOKING_FIXTURE_FAIL_PHASE = $priorPhase }
} finally { [IO.File]::WriteAllText($buildHelper, $buildHelperSource) }

$intake = Join-Path $workspace 'tests/Unit/Booking.ContractTests/IntakeCases.cs'
$original = [IO.File]::ReadAllText($intake)
$pattern = '(public void FirstImport\(\)\s*\{)'
Assert-Check ([regex]::Matches($original, $pattern).Count -eq 1) 'fixture method'
$sentinel = "synthetic-runner-probe-$([guid]::NewGuid().ToString('N'))"
try {
    $forbidden = @"
    private static void RunnerAttachmentProbe(TestContext context) {
        System.IO.File.WriteAllText("body-reached", "probe reached");
        context.AddResultFile("body-reached");
    }
"@
    $classPattern = '(public sealed partial class ContractCases\s*\{)'
    Assert-Check ([regex]::Matches($original, $classPattern).Count -eq 1) 'fixture class'
    [IO.File]::WriteAllText($intake, [regex]::Replace($original, $classPattern, ('$1' + $forbidden)))
    Invoke-RunnerCase 'Forbidden attachment before launch' 1
    Assert-Check ($caseManifest.phases.policy.status -ceq 'failed' -and $caseManifest.phases.test.status -ceq 'notRun' -and
        !(Test-Path -LiteralPath (Join-Path $caseRoot 'suite')) -and
        !(Test-Path -LiteralPath (Join-Path $workspace 'body-reached'))) 'policy rejection never launches tests'

    $failure = @'
        if (System.Environment.GetEnvironmentVariable("BOOKING_FAILURE_PROBE") is { } marker) {
            System.Console.WriteLine(marker);
            System.Console.Error.WriteLine(marker);
            Assert.Fail(marker);
        }
'@
    [IO.File]::WriteAllText($intake, [regex]::Replace($original, $pattern, ('$1' + $failure)))
    $priorProbe = $env:BOOKING_FAILURE_PROBE
    try {
        $env:BOOKING_FAILURE_PROBE = $sentinel
        Invoke-RunnerCase 'Returned test failure' 2
    } finally { $env:BOOKING_FAILURE_PROBE = $priorProbe }
    Assert-Check ($caseManifest.phases.test.exitCode -eq 2 -and $caseManifest.phases.evidence.status -ceq 'passed') 'native failure and safe evidence'
    Assert-Check (!$caseOutput.Contains($sentinel)) 'safe console'
    foreach ($directory in 'suite', 'export') {
        foreach ($record in Get-ChildItem -LiteralPath (Join-Path $caseRoot $directory) -File -Recurse) {
            Assert-Check (!(Get-Content -LiteralPath $record.FullName -Raw).Contains($sentinel)) 'safe retained evidence'
        }
    }
} finally { [IO.File]::WriteAllText($intake, $original) }

$artifactDirectory = Join-Path $caseRoot 'build/bin/Booking.ContractTests/release'
$dll = Join-Path $artifactDirectory 'Booking.ContractTests.dll'
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$bindingRun = Join-Path $workspace 'binding-check'
$null = New-Item -ItemType Directory -Path $bindingRun
$context = Read-EvidenceJson $caseRoot 'run.json'
$context.phases.test.status = 'running'
$contextPath = Join-Path $bindingRun 'run.json'
$suite = Join-Path $bindingRun 'suite'
$originalHash = $context.attachmentPolicy.assemblySha256[0]
$context.attachmentPolicy.assemblySha256[0] = '0' * 64
Write-EvidenceJson $contextPath $context
$child = Invoke-BackendProcess $dotnet @($dll, '--scenario-evidence', $suite, '--run-context', $contextPath) -TestProcess
$receipt = Read-EvidenceJson $bindingRun 'suite/execution.json'
Assert-Check ($child.ExitCode -eq 1 -and $receipt.failureCode -ceq 'invalid-run-context' -and
    $null -eq $receipt.platformExitCode -and !$receipt.policyVerified -and !$receipt.verificationEligible -and
    !(Test-Path -LiteralPath (Join-Path $suite 'scenario-results.json'))) 'mismatched policy digest cannot launch suite'
Write-Host 'Mismatched policy digest rejected'

$staleRun = Join-Path $workspace 'stale-check'
$null = New-Item -ItemType Directory -Path $staleRun
$staleContext = Join-Path $staleRun 'run.json'
$staleSuite = Join-Path $staleRun 'suite'
$context.attachmentPolicy.assemblySha256[0] = $originalHash
Write-EvidenceJson $staleContext $context
$catalog = Join-Path $artifactDirectory 'scenario-catalog.md'
$originalCatalog = [IO.File]::ReadAllBytes($catalog)
try {
    [IO.File]::AppendAllText($catalog, 'disposable mutation')
    $child = Invoke-BackendProcess $dotnet @($dll, '--scenario-evidence', $staleSuite, '--run-context', $staleContext) -TestProcess
    $receipt = Read-EvidenceJson $staleRun 'suite/execution.json'
    Assert-Check ($child.ExitCode -eq 1 -and $receipt.failureCode -ceq 'invalid-run-context' -and
        $null -eq $receipt.platformExitCode -and !$receipt.verificationEligible) 'stale output cannot launch suite'
} finally { [IO.File]::WriteAllBytes($catalog, $originalCatalog) }
Write-Host 'Stale copied fixture rejected'

try {
    [IO.File]::AppendAllText($buildHelper, @'

$originalBackendProcess = ${function:Invoke-BackendProcess}
function Invoke-BackendProcess {
    param([string]$Command, [string[]]$Arguments, [switch]$Capture, [switch]$TestProcess)
    if ($TestProcess) {
        if ($env:BOOKING_FIXTURE_ZERO_SELECTION -ceq 'yes') {
            $suiteDirectory = Join-Path $runDirectory 'suite'
            $null = [IO.Directory]::CreateDirectory($suiteDirectory)
            Write-EvidenceJson (Join-Path $suiteDirectory 'execution.json') @{
                schemaVersion = 1; platformExitCode = 0; testOutcome = 'passed'
                evidenceOutcome = 'incomplete'; failureCode = 'missing-test-results'
                runId = $manifest.runId; sourceDigest = $manifest.source.digest; joinedRunId = $null
                sourceVerified = $true; policyVerified = $true; verificationEligible = $false
            }
            return @{ ExitCode = 1; Output = $null }
        }
        return @{ ExitCode = 0; Output = $null }
    }
    & $originalBackendProcess $Command $Arguments -Capture:$Capture
}
'@)
    Invoke-RunnerCase 'Missing receipt cannot pass' 1
    Assert-Check ($caseManifest.phases.evidence.status -ceq 'failed' -and $caseManifest.phases.test.status -ceq 'failed' -and
        !(Test-Path -LiteralPath (Join-Path $caseRoot 'export/scenario-results.json'))) 'interrupted evidence export'
    $priorSelection = $env:BOOKING_FIXTURE_ZERO_SELECTION
    try {
        $env:BOOKING_FIXTURE_ZERO_SELECTION = 'yes'
        Invoke-RunnerCase 'Zero selected tests cannot pass' 1
        $zeroReceipt = Read-EvidenceJson $caseRoot 'export/execution.json'
        $exported = @(Get-ChildItem -LiteralPath (Join-Path $caseRoot 'export') -File).Name | Sort-Object
        Assert-Check ($caseManifest.phases.test.status -ceq 'passed' -and $caseManifest.phases.test.exitCode -eq 0 -and
            $caseManifest.phases.evidence.status -ceq 'failed' -and $zeroReceipt.platformExitCode -eq 0 -and
            $zeroReceipt.evidenceOutcome -ceq 'incomplete' -and !$zeroReceipt.verificationEligible -and
            ($exported -join ',') -ceq 'execution.json,run.json') 'zero selection preserves platform outcome and exports only safe metadata'
    } finally { $env:BOOKING_FIXTURE_ZERO_SELECTION = $priorSelection }
} finally { [IO.File]::WriteAllText($buildHelper, $buildHelperSource) }

$envOutput = Join-Path $workspace 'missing-output-parent/github-output.txt'
$outerOutput = $env:GITHUB_OUTPUT
try {
    $env:GITHUB_OUTPUT = $envOutput
    $run = Invoke-BackendProcess $shell @('-NoProfile', '-File', $runner) -Capture
    Assert-Check ($run.ExitCode -eq 1 -and $run.Output.Contains('export failed export-failed')) 'export failure is not a pass'
} finally { $env:GITHUB_OUTPUT = $outerOutput }
Write-Host 'Export failure path passed'

"Runner checks passed in disposable workspace: $workspace"
