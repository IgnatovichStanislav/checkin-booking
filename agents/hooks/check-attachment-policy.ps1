#Requires -Version 7
[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments)]
    [string[]] $AssemblyPath
)

$ErrorActionPreference = 'Stop'
try {
    $scannerSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'AttachmentPolicy.cs')
    $sourceBytes = [Text.Encoding]::UTF8.GetBytes($scannerSource)
    $scannerDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($sourceBytes))
    $scannerNamespace = "Booking.Verification.Policy_$scannerDigest"
    $scannerName = "$scannerNamespace.AttachmentPolicy"
    $scanner = $scannerName -as [type]
    if (-not $scanner) {
        $scannerSource = $scannerSource.Replace('namespace Booking.Verification;', "namespace $scannerNamespace;")
        Add-Type -TypeDefinition $scannerSource -WarningAction SilentlyContinue
        $scanner = $scannerName -as [type]
    }
    $resolvedPaths = @($AssemblyPath | ForEach-Object { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($_) })
    $verdict = $scanner::Inspect($resolvedPaths)
}
catch {
    $verdict = @{
        policyVersion = 1
        outcome = 'failed'
        failureCode = 'guard-error'
        assembliesChecked = 0
        methodsChecked = 0
        assemblySha256 = @()
    }
}

$verdict | ConvertTo-Json -Depth 3 -Compress
if ($verdict.outcome -eq 'passed') { exit 0 }
exit 1
