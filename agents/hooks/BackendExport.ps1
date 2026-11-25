function Read-EvidenceJson {
    param([string]$Root, [string]$Relative)

    $path = Assert-RegularPath $Root $Relative
    $file = Get-Item -LiteralPath $path -Force
    if ($file.PSIsContainer -or $file.Length -gt 8MB) { throw 'invalid-evidence-record' }
    return ,(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable -NoEnumerate)
}

function Write-EvidenceJson {
    param([string]$Path, $Value)

    $json = ConvertTo-Json -InputObject $Value -Depth 12
    [IO.File]::WriteAllText($Path, $json + [Environment]::NewLine)
}

function Test-GuidValue {
    param($Value)
    $parsed = [guid]::Empty
    return $Value -is [string] -and [guid]::TryParseExact($Value, 'D', [ref]$parsed) -and $parsed -ne [guid]::Empty
}

function Test-JsonInteger {
    param($Value)
    return ($Value -is [int] -or $Value -is [long]) -and $Value -ge 0 -and $Value -le [int]::MaxValue
}

function Read-SafeReceipt {
    param([int]$ProcessExit)

    $raw = Read-EvidenceJson $runDirectory 'suite/execution.json'
    $codes = @('invalid-run-context', 'platform-execution-incomplete', 'invalid-test-event', 'platform-result-mismatch',
        'missing-test-results', 'platform-tests-failed', 'invalid-scenario-evidence')
    if ($raw -isnot [Collections.IDictionary] -or $raw.schemaVersion -ne 1 -or
        @(@('sourceVerified', 'policyVerified', 'verificationEligible') | Where-Object { $raw[$_] -isnot [bool] }).Count) {
        throw 'invalid-execution-receipt'
    }
    if ($raw.testOutcome -cnotin @('pending', 'passed', 'failed', 'incomplete') -or
        $raw.evidenceOutcome -cnotin @('passed', 'failed', 'incomplete') -or
        ($null -ne $raw.failureCode -and $raw.failureCode -cnotin $codes) -or
        ($null -ne $raw.joinedRunId -and !(Test-GuidValue $raw.joinedRunId))) { throw 'invalid-execution-receipt' }

    $bindingMatches = $raw.runId -ceq $manifest.runId -and $raw.sourceDigest -ceq $manifest.source.digest
    if ($raw.failureCode -ceq 'invalid-run-context') {
        if ($raw.sourceVerified -or $raw.policyVerified -or $raw.verificationEligible -or $raw.runId -or $raw.sourceDigest -or
            $raw.testOutcome -cne 'pending' -or $raw.evidenceOutcome -cne 'failed' -or
            $null -ne $raw.platformExitCode -or $raw.joinedRunId -or $ProcessExit -ne 1) { throw 'invalid-execution-receipt' }
    } elseif (!$bindingMatches -or !$raw.sourceVerified -or !$raw.policyVerified) {
        throw 'invalid-execution-receipt'
    }
    $platformExit = $raw.platformExitCode
    if ($null -ne $platformExit) {
        if (!(Test-JsonInteger $platformExit) -or
            $raw.testOutcome -cne $(if ($platformExit -eq 0) { 'passed' } else { 'failed' })) { throw 'invalid-execution-receipt' }
    } elseif ($raw.testOutcome -cnotin @('pending', 'incomplete')) { throw 'invalid-execution-receipt' }
    $evidencePassed = $raw.evidenceOutcome -ceq 'passed'
    if ($evidencePassed -ne $raw.verificationEligible -or
        ($evidencePassed -and (!$raw.joinedRunId -or $null -eq $platformExit -or
            $raw.failureCode -cne $(if ($platformExit -ne 0) { 'platform-tests-failed' } else { $null }))) -or
        (!$evidencePassed -and !$raw.failureCode)) { throw 'invalid-execution-receipt' }
    $expectedExit = if ($platformExit) { $platformExit } elseif ($evidencePassed) { 0 } else { 1 }
    if ($ProcessExit -ne $expectedExit) { throw 'invalid-execution-receipt' }

    return [ordered]@{
        schemaVersion = 1; platformExitCode = $platformExit; testOutcome = $raw.testOutcome
        evidenceOutcome = $raw.evidenceOutcome; failureCode = $raw.failureCode
        runId = $manifest.runId; sourceDigest = $manifest.source.digest; joinedRunId = $raw.joinedRunId
        sourceVerified = $raw.sourceVerified; policyVerified = $raw.policyVerified; verificationEligible = $raw.verificationEligible
    }
}

function Select-EvidenceMethod {
    param($Method)

    if ($Method -isnot [Collections.IDictionary] -or $Method.assembly -cne 'Booking.ContractTests' -or
        $Method.class -cnotmatch '^Booking\.ContractTests\.[A-Za-z][A-Za-z0-9]*$' -or
        $Method.method -cnotmatch '^[A-Za-z][A-Za-z0-9]*$' -or
        $Method.intakePath -cnotin @($null, 'SAL', 'all') -or $Method.evidenceLevel -cnotin @('contract', 'runtime', 'harness') -or
        @($Method.scenarioIds -cnotmatch '^(INT|H01|SAL|CAS|BRK|KFK|PDS)-[0-9]{2}$').Count -or
        @($Method.featureCaseIds -cnotmatch '^A(0[1-9]|10)$').Count -or
        @($Method.requirementIds -cnotmatch '^FR-0[1-9]$').Count) { throw 'invalid-scenario-evidence' }
    foreach ($field in @('scenarioIds', 'featureCaseIds', 'requirementIds', 'fixtures')) {
        if ($Method[$field] -isnot [array]) { throw 'invalid-scenario-evidence' }
    }
    $fixtures = @($Method.fixtures | ForEach-Object {
        if ($_ -isnot [Collections.IDictionary] -or $_.path -cnotmatch
            '^e2e/fixtures/001-booking-contracts/(intake/(restricted/)?|attendance/|registration/|wire/)[A-Za-z0-9-]+\.json$' -or
            $_.caseIds -isnot [array] -or @($_.caseIds -cnotmatch '^[A-Za-z0-9][A-Za-z0-9-]*$').Count) { throw 'invalid-scenario-evidence' }
        [ordered]@{ path = $_.path; caseIds = $_.caseIds }
    })
    return [ordered]@{
        assembly = $Method.assembly; class = $Method.class; method = $Method.method
        intakePath = $Method.intakePath; evidenceLevel = $Method.evidenceLevel
        scenarioIds = $Method.scenarioIds; featureCaseIds = $Method.featureCaseIds; requirementIds = $Method.requirementIds; fixtures = $fixtures
    }
}

function Read-SafeResults {
    param($Receipt)

    $report = Read-EvidenceJson $runDirectory 'suite/scenario-results.json'
    $rawMap = Read-EvidenceJson $runDirectory 'suite/scenario-map.json'
    if ($report -isnot [Collections.IDictionary] -or $report.testRunId -cne $Receipt.joinedRunId -or
        $rawMap -isnot [array] -or $rawMap.Count -eq 0) { throw 'invalid-scenario-evidence' }
    $inventory = Read-EvidenceJson $artifactDirectory 'required-tests.json'
    $identities = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $inventory) {
        if (!$identities.Add("$($item.assembly.ToLowerInvariant())/$($item.class)/$($item.method)")) { throw 'invalid-scenario-evidence' }
    }
    $map = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $methods = @($rawMap | ForEach-Object {
        $method = Select-EvidenceMethod $_
        $key = "$($method.class)/$($method.method)"
        if (!$identities.Remove("$($method.assembly.ToLowerInvariant())/$key") -or !$map.TryAdd($key, $method)) { throw 'invalid-scenario-evidence' }
        $method
    })
    if ($identities.Count) { throw 'invalid-scenario-evidence' }
    $result = [ordered]@{ testRunId = $Receipt.joinedRunId }
    $failed = 0
    foreach ($group in @('businessEvidence', 'harnessChecks')) {
        if ($report[$group] -isnot [array]) { throw 'invalid-scenario-evidence' }
        $result[$group] = @(foreach ($row in $report[$group]) {
            if ($row -isnot [Collections.IDictionary] -or
                !(($row.status -ceq 'passed' -and $row.trxOutcome -ceq 'Passed') -or
                 ($row.status -ceq 'failed' -and $row.trxOutcome -cin @('Failed', 'Error', 'Timeout', 'Aborted', 'NotRunnable', 'PassedButRunAborted')))) {
                throw 'invalid-scenario-evidence'
            }
            $method = Select-EvidenceMethod $row.method
            $key = "$($method.class)/$($method.method)"
            if (!$map.ContainsKey($key) -or ($method.evidenceLevel -ceq 'harness') -ne ($group -ceq 'harnessChecks') -or
                (ConvertTo-Json -InputObject $method -Depth 8 -Compress) -cne
                (ConvertTo-Json -InputObject $map[$key] -Depth 8 -Compress)) { throw 'invalid-scenario-evidence' }
            $null = $map.Remove($key)
            if ($row.status -ceq 'failed') { $failed++ }
            [ordered]@{ method = $method; trxOutcome = $row.trxOutcome; status = $row.status }
        })
    }
    if ($map.Count -or ($failed -gt 0) -ne ($Receipt.platformExitCode -ne 0)) { throw 'invalid-scenario-evidence' }
    return @{ Map = $methods; Results = $result; Passed = $methods.Count - $failed; Failed = $failed }
}

function Write-SafeExport {
    $directory = Assert-RegularPath $runDirectory 'export'
    if ([IO.Path]::Exists($directory)) { throw 'export-directory-exists' }
    $null = [IO.Directory]::CreateDirectory($directory)
    $sourceDigest = $manifest.source.digest
    if ($safeReceipt) { Write-EvidenceJson (Join-Path $directory 'execution.json') $safeReceipt }
    if ($safeResults -and $manifest.phases.evidence.status -ceq 'passed') {
        Write-EvidenceJson (Join-Path $directory 'scenario-map.json') ([ordered]@{
            runId = $manifest.runId; sourceDigest = $sourceDigest; methods = $safeResults.Map
        })
        $report = [ordered]@{ runId = $manifest.runId; sourceDigest = $sourceDigest }
        foreach ($key in $safeResults.Results.Keys) { $report[$key] = $safeResults.Results[$key] }
        Write-EvidenceJson (Join-Path $directory 'scenario-results.json') $report
    }
    return $directory
}
