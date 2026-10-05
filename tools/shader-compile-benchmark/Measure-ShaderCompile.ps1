param(
    [string] $UnityExe = 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe',
    [string] $PackageSource = (Join-Path $PSScriptRoot '../../Assets/lilToon'),
    [string] $ValidationProject = 'D:/Unity_Fork/lilToon-URP-Extensions/.codex-research/CharacterShadowValidation',
    [string] $PlayerSettingsSource = 'D:/Unity_Project/BREAK_URP/ProjectSettings/ProjectSettings.asset',
    [string] $OutputRoot = 'D:/Unity_Fork/lilToon-URP-Extensions/.codex-research/b'
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$runPath = Join-Path $OutputRoot ([DateTime]::UtcNow.ToString('MMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 4))
$projectPath = Join-Path $runPath 'Project'
$snapshotPath = Join-Path $runPath 'lilToon'
New-Item -ItemType Directory -Path $projectPath,$snapshotPath,(Join-Path $projectPath 'Assets/Editor'),(Join-Path $projectPath 'Packages') -Force | Out-Null
# A unique copy and fresh Library keep source changes/cache operations out of the active project.
Copy-Item -Path (Join-Path $PackageSource '*') -Destination $snapshotPath -Recurse
Copy-Item -LiteralPath (Join-Path $ValidationProject 'ProjectSettings') -Destination $projectPath -Recurse
if (Test-Path -LiteralPath $PlayerSettingsSource) {
    $colorSpaceMatch = [regex]::Match([IO.File]::ReadAllText($PlayerSettingsSource), '(?m)^\s*m_ActiveColorSpace:\s*(\d+)')
    if (-not $colorSpaceMatch.Success) { throw 'Source color space not found' }
    $copiedSettings = Join-Path $projectPath 'ProjectSettings/ProjectSettings.asset'
    $text = [IO.File]::ReadAllText($copiedSettings)
    $text = [regex]::Replace($text, '(?m)^(\s*m_ActiveColorSpace:)\s*\d+', ('$1 ' + $colorSpaceMatch.Groups[1].Value))
    [IO.File]::WriteAllText($copiedSettings, $text, $utf8)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ShaderCompileBenchmark.cs') -Destination (Join-Path $projectPath 'Assets/Editor/ShaderCompileBenchmark.cs')
$manifest = Get-Content -LiteralPath (Join-Path $ValidationProject 'Packages/manifest.json') -Raw | ConvertFrom-Json
$manifest.dependencies.'jp.lilxyzw.liltoon' = 'file:' + $snapshotPath.Replace('\','/')
$extensionsSource = $manifest.dependencies.'jp.lilxyzw.liltoon.urp.extensions'.Substring(5)
$extensionsSnapshot = Join-Path $runPath 'Extensions'
New-Item -ItemType Directory -Path $extensionsSnapshot | Out-Null
# Freeze committed Extensions code. Unrelated in-progress edits can be temporarily
# uncompilable and must not change while this timing project is running.
$extensionsArchive = Join-Path $runPath 'Extensions.tar'
& git -C $extensionsSource archive --format=tar ('--output=' + $extensionsArchive) HEAD -- Runtime Editor package.json
if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot committed Extensions' }
& tar -xf $extensionsArchive -C $extensionsSnapshot
if ($LASTEXITCODE -ne 0) { throw 'Could not extract Extensions snapshot' }
$manifest.dependencies.'jp.lilxyzw.liltoon.urp.extensions' = 'file:' + $extensionsSnapshot.Replace('\','/')
# Reuse installed registry dependencies as local packages. This prevents network/package
# extraction time and Windows Package Manager rename failures from dominating the run.
$installedPackages = Join-Path $ValidationProject 'Library/PackageCache'
foreach ($directory in (Get-ChildItem -LiteralPath $installedPackages -Directory)) {
    $packageJson = Join-Path $directory.FullName 'package.json'
    if (-not (Test-Path -LiteralPath $packageJson)) { continue }
    $packageName = (Get-Content -LiteralPath $packageJson -Raw | ConvertFrom-Json).name
    $existing = $manifest.dependencies.PSObject.Properties[$packageName]
    if ($existing -and $existing.Value.StartsWith('file:')) { continue }
    $manifest.dependencies | Add-Member -NotePropertyName $packageName -NotePropertyValue ('file:' + $directory.FullName.Replace('\','/')) -Force
}
[IO.File]::WriteAllText((Join-Path $projectPath 'Packages/manifest.json'), ($manifest | ConvertTo-Json -Depth 20), $utf8)
$metadata = [ordered]@{
    packageSource = (Resolve-Path $PackageSource).Path
    sourceRevision = (& git -C (Join-Path $PackageSource '../..') rev-parse HEAD)
    sourceStatus = @(& git -C (Join-Path $PackageSource '../..') status --short)
    project = $projectPath
    startedUtc = [DateTime]::UtcNow.ToString('O')
    existingUnityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,CPU)
    freshProjectLocalCache = -not (Test-Path -LiteralPath (Join-Path $projectPath 'Library'))
    installedDependencyCache = $installedPackages
    playerSettingsSource = $PlayerSettingsSource
    extensionRevision = (& git -C $extensionsSource rev-parse HEAD)
    extensionSourceStatus = @(& git -C $extensionsSource status --short)
    benchmarkHarnessSHA256 = (Get-FileHash -LiteralPath (Join-Path $projectPath 'Assets/Editor/ShaderCompileBenchmark.cs') -Algorithm SHA256).Hash
}
[IO.File]::WriteAllText((Join-Path $runPath 'Run.json'), ($metadata | ConvertTo-Json -Depth 8), $utf8)
Write-Output "Benchmark project: $projectPath"
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"' + $projectPath + '"'),'-executeMethod','ShaderCompileBenchmark.Run','-logFile',('"' + (Join-Path $runPath 'Editor.log') + '"')) -WindowStyle Hidden -PassThru
Write-Output "Unity process: $($process.Id)"
$process.WaitForExit()
$metadata.processSeconds = $timer.Elapsed.TotalSeconds
$metadata.exitCode = $process.ExitCode
[IO.File]::WriteAllText((Join-Path $runPath 'Run.json'), ($metadata | ConvertTo-Json -Depth 8), $utf8)
Write-Output "Exit code: $($process.ExitCode); entire process: $($metadata.processSeconds) seconds"
Write-Output "Measurements: $(Join-Path $projectPath 'CompileBenchmark.json')"
if ($process.ExitCode -ne 0) { throw "Benchmark failed; inspect $(Join-Path $runPath 'Editor.log')" }
