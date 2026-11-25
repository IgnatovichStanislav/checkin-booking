#Requires -Version 7
[CmdletBinding()]
param(
    [string] $BuildDirectory = (Join-Path $PSScriptRoot '../../tests/Unit/Booking.ContractTests/bin/Debug/net10.0')
)

$ErrorActionPreference = 'Stop'
$guard = Join-Path $PSScriptRoot 'check-attachment-policy.ps1'
$verdictFields = 'assembliesChecked,assemblySha256,failureCode,methodsChecked,outcome,policyVersion'
$casesChecked = 0

function Assert-PolicyCase([string] $Name, [string[]] $InputPaths, [string] $ExpectedFailure) {
    $json = & $guard -AssemblyPath $InputPaths
    $guardExit = $LASTEXITCODE
    $verdict = $json | ConvertFrom-Json
    $expectedExit = if ($ExpectedFailure) { 1 } else { 0 }
    $expectedOutcome = if ($ExpectedFailure) { 'failed' } else { 'passed' }
    $expectedHashes = @($InputPaths | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() })
    if ($guardExit -ne $expectedExit -or $verdict.policyVersion -ne 1 -or $verdict.outcome -ne $expectedOutcome -or
        [string]$verdict.failureCode -cne $ExpectedFailure -or
        (($verdict.PSObject.Properties.Name | Sort-Object) -join ',') -cne $verdictFields -or
        ($verdict.assemblySha256 -join ',') -cne ($expectedHashes -join ',')) {
        throw "Unexpected policy verdict for $Name."
    }
    if (!$ExpectedFailure -and ($verdict.assembliesChecked -ne $InputPaths.Length -or $verdict.methodsChecked -le 0)) {
        throw "Incomplete policy inspection for $Name."
    }
    "$Name $expectedOutcome"
}

$testAssemblyNames = 'Booking.ContractTests', 'Booking.Application', 'Booking.Contracts', 'Booking.Domain'
$testModules = @($testAssemblyNames | ForEach-Object { Join-Path $BuildDirectory "$_.dll" })
$resolvedBuild = (Resolve-Path -LiteralPath $BuildDirectory).ProviderPath
$testFramework = Join-Path $resolvedBuild 'MSTest.TestFramework.dll'
$testFrameworkExtensions = Join-Path $resolvedBuild 'MSTest.TestFramework.Extensions.dll'
if (!(Test-Path -LiteralPath $testFramework -PathType Leaf) -or !(Test-Path -LiteralPath $testFrameworkExtensions -PathType Leaf)) {
    throw 'Build the backend test project before running the policy checks.'
}

Assert-PolicyCase -Name 'OwnedModules' -InputPaths $testModules -ExpectedFailure ''
$casesChecked++
Assert-PolicyCase -Name 'MissingOwnedReference' -InputPaths @($testModules[0]) -ExpectedFailure 'unresolved-metadata'
$casesChecked++

$ignoredRoot = New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot '../../test-results')
$workspace = Join-Path $ignoredRoot.FullName ("attachment-policy-" + [guid]::NewGuid())
$null = New-Item -ItemType Directory $workspace
$frameworkXml = [Security.SecurityElement]::Escape($testFramework)
$extensionsXml = [Security.SecurityElement]::Escape($testFrameworkExtensions)
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="MSTest.TestFramework" HintPath="$frameworkXml" />
    <Reference Include="MSTest.TestFramework.Extensions" HintPath="$extensionsXml" />
  </ItemGroup>
</Project>
"@

$fixtures = [ordered]@{
    DirectCall = @{
        code = 'public void Probe() { MarkReached(); Context.AddResultFile("unused.txt"); }'
        expected = 'forbidden-attachment'
    }
    OwnedHelper = @{
        code = 'public void Probe() { MarkReached(); Helper(); } private void Helper() => Context.AddResultFile("unused.txt");'
        expected = 'forbidden-attachment'
    }
    OwnedConstructor = @{
        code = 'public void Probe() => MarkReached(); public ProbeTests() { MarkReached(); Context.AddResultFile("unused.txt"); }'
        expected = 'forbidden-attachment'
    }
    GeneratedAsyncBody = @{
        code = 'public async Task Probe() { await Task.Yield(); MarkReached(); Context.AddResultFile("unused.txt"); }'
        expected = 'forbidden-attachment'
    }
    AttachmentFunctionTarget = @{
        code = 'public void Probe() { MarkReached(); Action<string> sink = Context.AddResultFile; GC.KeepAlive(sink); }'
        expected = 'forbidden-attachment'
    }
    ReflectionInvocation = @{
        code = 'public void Probe() { MarkReached(); typeof(ProbeTests).GetMethod(nameof(Probe)).Invoke(this, null); }'
        expected = 'unsupported-execution'
    }
    IndirectFunctionCall = @{
        code = 'public unsafe void Probe() { MarkReached(); delegate* managed<int, int> call = &Identity; GC.KeepAlive(call(1)); } private static int Identity(int value) => value;'
        expected = 'unsupported-execution'
    }
    DynamicInvocation = @{
        code = 'public void Probe() { MarkReached(); dynamic candidate = Context; candidate.ToString(); }'
        expected = 'unsupported-execution'
    }
    CustomExecutor = @{
        code = 'public void Probe() => MarkReached();'
        attribute = '[CustomTest]'
        additional = 'public sealed class CustomTestAttribute : TestMethodAttribute { }'
        expected = 'unsupported-execution'
    }
    LifecycleAttribute = @{
        code = 'public void Probe() => MarkReached(); [TestInitialize] public void Setup() => MarkReached();'
        expected = 'unsupported-execution'
    }
    DataDrivenMethod = @{
        code = '[DataRow(1)] public void Probe(int value) => MarkReached();'
        expected = 'unsupported-execution'
    }
    InheritedTestClass = @{
        code = 'public void Probe() => MarkReached();'
        parent = ' : ProbeBase'
        additional = 'public class ProbeBase { public void Inherited() { } }'
        expected = 'unsupported-execution'
    }
    ModuleInitializer = @{
        code = 'public void Probe() => MarkReached(); [System.Runtime.CompilerServices.ModuleInitializer] internal static void Initialize() => MarkReached();'
        expected = 'unsupported-execution'
    }
}

foreach ($name in $fixtures.Keys) {
    $fixture = $fixtures[$name]
    $caseRoot = Join-Path $workspace $name
    $null = New-Item -ItemType Directory $caseRoot
    $projectPath = Join-Path $caseRoot "$name.csproj"
    Set-Content -LiteralPath $projectPath -Value $project
    $attribute = $fixture.attribute ?? '[TestMethod]'
    $source = @"
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
$($fixture.additional)

[TestClass]
public sealed class ProbeTests$($fixture.parent)
{
    public TestContext Context { get; set; }
    private static void MarkReached() => File.WriteAllText("body-reached", "probe body executed");
    public static int Main() { MarkReached(); return 41; }
    $attribute
    $($fixture.code)
}
"@
    Set-Content -LiteralPath (Join-Path $caseRoot 'Probe.cs') -Value $source
    if (Get-Command rtk -ErrorAction SilentlyContinue) {
        $null = & rtk dotnet build $projectPath --nologo --verbosity quiet 2>&1
    }
    else {
        $null = & dotnet build $projectPath --nologo --verbosity quiet 2>&1
    }
    if ($LASTEXITCODE -ne 0) { throw "Could not compile disposable case $name." }

    $assembly = Join-Path $caseRoot "bin/Debug/net10.0/$name.dll"
    Push-Location $caseRoot
    try {
        Assert-PolicyCase -Name $name -InputPaths @($assembly) -ExpectedFailure $fixture.expected
        if ([AppDomain]::CurrentDomain.GetAssemblies().GetName().Name -contains $name -or
            (Test-Path -LiteralPath 'body-reached') -or
            (Get-ChildItem -Recurse -Directory -Filter 'MSTest-*') -or
            (Get-ChildItem -Recurse -File -Include 'execution.json', 'unused.txt', '*.trx')) {
            throw "Disposable case $name loaded or reached platform execution or staged output."
        }
        $casesChecked++
    }
    finally {
        Pop-Location
    }
}

$malformed = Join-Path $workspace 'malformed.dll'
[IO.File]::WriteAllBytes($malformed, [byte[]] @(0x4d, 0x5a, 0, 0))
Assert-PolicyCase -Name 'MalformedPe' -InputPaths @($malformed) -ExpectedFailure 'invalid-metadata'
$casesChecked++

$malformedMetadata = Join-Path $workspace 'malformed-metadata.dll'
$validImage = Join-Path $workspace 'DirectCall/bin/Debug/net10.0/DirectCall.dll'
$imageBytes = [IO.File]::ReadAllBytes($validImage)
$pe = [System.Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($imageBytes, $false))
try {
    $imageBytes[$pe.PEHeaders.MetadataStartOffset] = 0
}
finally {
    $pe.Dispose()
}
[IO.File]::WriteAllBytes($malformedMetadata, $imageBytes)
Assert-PolicyCase -Name 'MalformedMetadata' -InputPaths @($malformedMetadata) -ExpectedFailure 'invalid-metadata'
$casesChecked++

$cacheDirectory = Join-Path $workspace 'scanner-cache'
$null = New-Item -ItemType Directory $cacheDirectory
$copiedGuard = Join-Path $cacheDirectory 'check-attachment-policy.ps1'
$copiedSource = Join-Path $cacheDirectory 'AttachmentPolicy.cs'
Copy-Item -LiteralPath $guard -Destination $copiedGuard
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AttachmentPolicy.cs') -Destination $copiedSource
$valid = & $copiedGuard -AssemblyPath $testModules
if ($LASTEXITCODE -ne 0 -or ($valid | ConvertFrom-Json).outcome -cne 'passed') {
    throw 'The same-session guard failed to inspect the valid inputs again.'
}
$before = Get-Content -Raw -LiteralPath $copiedSource
Set-Content -LiteralPath $copiedSource -Value "$before`ninvalid helper content"
$invalid = & $copiedGuard -AssemblyPath $testModules
$invalidExit = $LASTEXITCODE
Set-Content -LiteralPath $copiedSource -Value $before -NoNewline
$invalidVerdict = $invalid | ConvertFrom-Json
if ($invalidExit -ne 1 -or $invalidVerdict.outcome -cne 'failed' -or $invalidVerdict.failureCode -cne 'guard-error' -or
    (($invalidVerdict.PSObject.Properties.Name | Sort-Object) -join ',') -cne $verdictFields -or
    $invalidVerdict.assembliesChecked -ne 0 -or $invalidVerdict.methodsChecked -ne 0) {
    throw 'The same-session guard reused stale source or disclosed an unsafe compilation error.'
}
$casesChecked++
"ChangedScannerSource failed"
"Attachment policy checks passed: $casesChecked"
