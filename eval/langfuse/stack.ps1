#requires -Version 7.2
[CmdletBinding()]
param([ValidateSet('start','stop','status','validate','recreate')] [string]$Action = 'status')
$ErrorActionPreference = 'Stop'
$common = git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve main checkout.' }
$main = Split-Path $common
$dataRoot = $env:LANGFUSE_DATA_DIR
if (!$dataRoot) { $dataRoot = Join-Path (Split-Path $main) ((Split-Path $main -Leaf) + '_Langfuse') }
if (![IO.Path]::IsPathFullyQualified($dataRoot)) { throw 'LANGFUSE_DATA_DIR must be absolute.' }
$env:LANGFUSE_DATA_DIR = [IO.Path]::GetFullPath($dataRoot).Replace('\','/')
$secretDir = Join-Path $main 'secrets'
$secretFile = Join-Path $secretDir 'langfuse.env'
if (!(Test-Path -LiteralPath $secretFile)) {
    if ($Action -ne 'start') { throw 'Initialize with stack.ps1 start first.' }
    if ((Test-Path -LiteralPath $dataRoot) -and (Get-ChildItem -LiteralPath $dataRoot -Force | Select-Object -First 1)) {
        throw 'Existing storage found. Restore the corresponding secrets/langfuse.env before starting.'
    }
    [void][IO.Directory]::CreateDirectory($secretDir)
    $lines = foreach ($name in 'PG_PASSWORD','CH_PASSWORD','REDIS_PASSWORD','S3_PASSWORD','SALT','ENCRYPTION_KEY','NEXTAUTH_SECRET','PUBLIC_KEY','SECRET_KEY','OWNER_PASSWORD') {
        $value = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
        if ($name -eq 'PUBLIC_KEY') { $value = "pk-lf-$value" }
        if ($name -eq 'SECRET_KEY') { $value = "sk-lf-$value" }
        "$name=$value"
    }
    $stream = [IO.File]::Open($secretFile, 'CreateNew', 'Write', 'None')
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n") + "`n")
        $stream.Write($bytes,0,$bytes.Length)
    } finally { $stream.Dispose() }
}
$argsCompose = @('compose','--project-name','checkin-langfuse','--env-file',$secretFile,'-f',(Join-Path $PSScriptRoot 'compose.yaml'))
switch ($Action) {
    'start'    { $operation = @('up','-d','--wait','--wait-timeout','300') }
    'recreate' { $operation = @('up','-d','--force-recreate','--wait','--wait-timeout','300') }
    'stop'     { $operation = @('stop') }
    'status'   { $operation = @('ps') }
    'validate' { $operation = @('config','--quiet') }
}
Write-Host "Langfuse storage: $dataRoot"
& docker @argsCompose @operation
exit $LASTEXITCODE
