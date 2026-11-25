$testProject = 'tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj'
$ownedAssemblies = @('Booking.ContractTests.dll', 'Booking.Application.dll', 'Booking.Contracts.dll', 'Booking.Domain.dll')
$rootInputs = @('global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config', 'nuget.config')
$gitScope = @('src', 'tests', 'e2e/fixtures', 'agents/hooks', '.github/workflows', ':(glob)specs/archive/001-booking-contracts/contracts/*.json') + $rootInputs
$generatedPath = '(?i)(^|/)(bin|obj|test-results|TestResults|secrets)(/|$)'

function Invoke-BackendProcess {
    param([string]$Command, [string[]]$Arguments, [switch]$Capture, [switch]$TestProcess)

    $start = [Diagnostics.ProcessStartInfo]::new($Command)
    $start.WorkingDirectory = $projectRoot
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false, $true)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    if ($TestProcess) {
        foreach ($key in @($start.Environment.Keys)) {
            if ($key -match '^(DOTNET_(STARTUP_HOOKS|ADDITIONAL_DEPS|SHARED_STORE|ROOT.*|HOST_PATH)|COREHOST_.*|DOTNET_HOST_TRACE.*|CORECLR_.*|COR_.*|COMPlus_.*)$') {
                $null = $start.Environment.Remove($key)
            }
        }
    } else {
        foreach ($key in @($start.Environment.Keys)) {
            if ($key -match '^(MSBuild|Custom(Before|After)|DirectoryBuild|DirectoryPackages|ImportDirectory)') {
                $null = $start.Environment.Remove($key)
            }
        }
    }

    $process = [Diagnostics.Process]::Start($start)
    try {
        $stderr = $process.StandardError.BaseStream.CopyToAsync([IO.Stream]::Null)
        $stdout = if ($Capture) { $process.StandardOutput.ReadToEndAsync() }
            else { $process.StandardOutput.BaseStream.CopyToAsync([IO.Stream]::Null) }
        $process.WaitForExit()
        $null = $stderr.GetAwaiter().GetResult()
        $output = $stdout.GetAwaiter().GetResult()
        return @{ ExitCode = $process.ExitCode; Output = $output }
    } finally {
        $process.Dispose()
    }
}

function Invoke-BuildCommand {
    param([string[]]$Arguments)

    if ($rtkCommand) {
        return Invoke-BackendProcess $rtkCommand.Source (@('dotnet') + $Arguments)
    }
    return Invoke-BackendProcess $dotnetCommand.Source $Arguments
}

function Read-GitOutput {
    param([string[]]$Arguments)

    $result = Invoke-BackendProcess $gitCommand.Source $Arguments -Capture
    if ($result.ExitCode -ne 0) { throw 'source-unavailable' }
    return $result.Output
}

function Get-DigestBytes {
    param([string]$Path)

    $file = [IO.File]::OpenRead($Path)
    try { return ,([Security.Cryptography.SHA256]::HashData($file)) }
    finally { $file.Dispose() }
}

function Assert-RegularPath {
    param([string]$Root, [string]$Relative)

    if ($Relative -match '\\|(^|/)\.{1,2}(/|$)|[\x00-\x1F]' -or [IO.Path]::IsPathRooted($Relative)) {
        throw 'unsafe-input-path'
    }
    $path = $Root
    foreach ($segment in $Relative.Split('/')) {
        $path = Join-Path $path $segment
        $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'linked-input-path' }
    }
    return $path
}

function Get-SourceSnapshot {
    $commit = (Read-GitOutput @('rev-parse', '--verify', 'HEAD')).Trim()
    if ($commit -cnotmatch '^(?:[0-9a-f]{40}|[0-9a-f]{64})$') { throw 'source-unavailable' }
    $dirty = [bool](Read-GitOutput @('status', '--porcelain=v1', '--untracked-files=normal', '-z'))
    $selected = Read-GitOutput (@('ls-files', '--cached', '--others', '--exclude-standard', '-z', '--') + $gitScope)
    $ignored = Read-GitOutput (@('ls-files', '--others', '--ignored', '--exclude-standard', '-z', '--') + $gitScope)
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($relative in $selected.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($relative -notmatch $generatedPath) { $null = $paths.Add($relative) }
    }
    foreach ($relative in $ignored.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($relative -notmatch $generatedPath -and ($relative -match '^(src|tests|e2e/fixtures|specs/archive)/' -or
                $relative -cin $rootInputs -or $relative -cmatch '^agents/hooks/(check-attachment-policy.ps1|AttachmentPolicy.cs)$')) {
            throw 'ignored-build-input'
        }
    }
    [string[]]$orderedPaths = @($paths)
    [Array]::Sort($orderedPaths, [StringComparer]::Ordinal)
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($relative in $orderedPaths) {
            $path = Assert-RegularPath $projectRoot $relative
            $present = [IO.File]::Exists($path)
            if (!$present -and [IO.Directory]::Exists($path)) { throw 'unexpected-input-directory' }
            $hash.AppendData([Text.Encoding]::UTF8.GetBytes("$relative`0"))
            $hash.AppendData([byte[]]@([int]$present))
            if ($present) { $hash.AppendData((Get-DigestBytes $path)) }
        }
        return @{
            Source = @{ commit = $commit; dirty = $dirty; scopeVersion = 1; digest = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant() }
            Paths = $paths
        }
    } finally {
        $hash.Dispose()
    }
}

function Assert-BuildInputs {
    param($Paths)

    $parent = [IO.Directory]::GetParent($projectRoot)
    while ($parent) {
        foreach ($name in 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props') {
            if ([IO.Path]::Exists((Join-Path $parent.FullName $name))) { throw 'unexpected-build-import' }
        }
        $parent = $parent.Parent
    }
    $projects = @($testProject, 'src/Booking.Application/Booking.Application.csproj',
        'src/Booking.Contracts/Booking.Contracts.csproj', 'src/Booking.Domain/Booking.Domain.csproj')
    $buildFiles = @($Paths | Where-Object { $_ -match '\.(csproj|props|targets)$' })
    foreach ($relative in ($projects + $buildFiles | Select-Object -Unique)) {
        if ($relative -cnotin ($projects + $rootInputs)) { throw 'unexpected-build-import' }
        $path = Join-Path $projectRoot $relative
        if (![IO.File]::Exists($path) -and $relative -cin $rootInputs) { continue }
        $xml = [Xml.XmlDocument]::new()
        $xml.Load($path)
        $sdk = $xml.DocumentElement.GetAttribute('Sdk')
        if ($xml.DocumentType -or $xml.DocumentElement.Name -cne 'Project' -or
            $sdk -cne $(if ($relative -ceq $testProject) { 'MSTest.Sdk/4.5.3' } elseif ($relative -cin $projects) { 'Microsoft.NET.Sdk' } else { '' })) {
            throw 'unexpected-build-import'
        }
        foreach ($node in $xml.SelectNodes('//*')) {
            $location = "$($node.ParentNode.Name)/$($node.Name)"
            switch -CaseSensitive -Regex ($location) {
                '^#document/Project$' { $allowed = @('Sdk'); break }
                '^Project/(PropertyGroup|ItemGroup)$' { $allowed = @(); break }
                '^PropertyGroup/(TargetFramework|LangVersion|Nullable|ImplicitUsings|RestorePackagesWithLockFile|StartupObject|TreatWarningsAsErrors|DefineConstants)$' {
                    $allowed = @()
                    if ($node.InnerText -notmatch '^[a-zA-Z0-9_.;]+$') { throw 'unexpected-build-import' }
                    break
                }
                '^ItemGroup/ProjectReference$' {
                    $allowed = @('Include')
                    $reference = $node.GetAttribute('Include')
                    $resolved = [IO.Path]::GetFullPath($reference, [IO.Path]::GetDirectoryName($path))
                    $target = [IO.Path]::GetRelativePath($projectRoot, $resolved).Replace('\', '/')
                    if ($reference -notmatch '^([.]{2}/)+[a-zA-Z0-9./-]+\.csproj$' -or $target -cnotin $projects) {
                        throw 'unexpected-build-import'
                    }
                    break
                }
                '^ItemGroup/None$' {
                    $allowed = @('Include', 'Update', 'Link', 'CopyToOutputDirectory')
                    if ($relative -cne $testProject) { throw 'unexpected-build-import' }
                    $items = @{
                        '../../../e2e/fixtures/001-booking-contracts/intake/**/*.json' = 'fixtures/intake/%(RecursiveDir)%(Filename)%(Extension)'
                        '../../../e2e/fixtures/001-booking-contracts/attendance/*.json' = 'fixtures/attendance/%(Filename)%(Extension)'
                        '../../../e2e/fixtures/001-booking-contracts/registration/*.json' = 'fixtures/registration/%(Filename)%(Extension)'
                        '../../../e2e/fixtures/001-booking-contracts/wire/*.json' = 'fixtures/wire/%(Filename)%(Extension)'
                        '../../../specs/archive/001-booking-contracts/contracts/*.json' = 'contracts/%(Filename)%(Extension)'
                        '../../../e2e/fixtures/scenarios.md' = 'scenario-catalog.md'
                    }
                    $include = $node.GetAttribute('Include')
                    $update = $node.GetAttribute('Update')
                    $link = $node.GetAttribute('Link')
                    if ($node.GetAttribute('CopyToOutputDirectory') -cne 'PreserveNewest' -or
                        !(($include -cin $items.Keys -and !$update -and $items[$include] -ceq $link) -or
                        (!$include -and $update -ceq 'required-tests.json' -and !$link))) { throw 'unexpected-build-import' }
                    break
                }
                '^ItemGroup/TestingPlatformBuilderHook$' {
                    $allowed = @('Include')
                    if ($relative -cne $testProject -or $node.GetAttribute('Include') -cne '97f32ea1-2d18-4c29-ad48-60552aa1e502') {
                        throw 'unexpected-build-import'
                    }
                    break
                }
                '^TestingPlatformBuilderHook/DisplayName$' { $allowed = @(); break }
                '^TestingPlatformBuilderHook/TypeFullName$' {
                    $allowed = @()
                    if ($node.InnerText -cne 'Booking.ContractTests.SafeEvidenceBuilderHook') { throw 'unexpected-build-import' }
                    break
                }
                default { throw 'unexpected-build-import' }
            }
            if (@($node.Attributes | Where-Object { $_.Name -cnotin $allowed }).Count) { throw 'unexpected-build-import' }
        }
    }
}

function Get-ArtifactDigest {
    param([string]$Directory)

    if (![IO.Directory]::Exists($Directory) -or (Get-Item -LiteralPath $Directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'invalid-artifacts'
    }
    [string[]]$paths = @(Get-ChildItem -LiteralPath $Directory -Recurse -Force | ForEach-Object {
        if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'invalid-artifacts' }
        if (!$_.PSIsContainer) { [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/') }
    })
    $required = $ownedAssemblies + @('Booking.ContractTests.deps.json', 'Booking.ContractTests.runtimeconfig.json', 'required-tests.json', 'scenario-catalog.md')
    if (@($required | Where-Object { $_ -cnotin $paths }).Count -or
        !($paths -cmatch '^fixtures/.+\.json$') -or !($paths -cmatch '^contracts/[^/]+\.json$')) { throw 'invalid-artifacts' }
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($relative in $paths) {
            $hash.AppendData([Text.Encoding]::UTF8.GetBytes("$relative`0"))
            $hash.AppendData((Get-DigestBytes (Join-Path $Directory $relative)))
        }
        return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    } finally {
        $hash.Dispose()
    }
}
