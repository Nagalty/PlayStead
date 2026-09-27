[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [ValidateSet('alpha','stable')][string]$Channel = 'alpha',
    [string]$PackageUrl = '',
    [string]$ReleaseNotesUrl = '',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$packageUrlForManifest = if ($PackageUrl) { $PackageUrl } else { "https://example.invalid/PlayStead-$Version-$RuntimeIdentifier.zip" }
$releaseNotesUrlForManifest = if ($ReleaseNotesUrl) { $ReleaseNotesUrl } else { "https://example.invalid/PlayStead/$Version/release-notes" }
$repo = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repo 'artifacts\github'
$work = Join-Path $artifactRoot "work-$Version-$RuntimeIdentifier"
$zip = Join-Path $artifactRoot "PlayStead-$Version-$RuntimeIdentifier.zip"
$sha = "$zip.sha256"
$manifest = Join-Path $artifactRoot "PlayStead-$Version-manifest.json"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work,$artifactRoot,(Join-Path $work 'app'),(Join-Path $work 'updater') -Force | Out-Null

if (-not $SkipBuild) {
    dotnet publish (Join-Path $repo 'src\PlayStead.UI\PlayStead.UI.csproj') -c Release -r $RuntimeIdentifier --self-contained true -p:SelfContained=true -p:RestoreUseStaticGraphEvaluation=true -p:NuGetAudit=false -p:PlaySteadDistributionChannel=GitHub -p:PlaySteadProductVersion=$Version -p:Version=$Version -m:1 -o (Join-Path $work 'app')
    dotnet publish (Join-Path $repo 'src\PlayStead.Updater\PlayStead.Updater.csproj') -c Release -r $RuntimeIdentifier --self-contained true -p:SelfContained=true -p:NuGetAudit=false -p:PlaySteadProductVersion=$Version -p:Version=$Version -m:1 -o (Join-Path $work 'updater')
} else {
    Copy-Item (Join-Path $repo 'src\PlayStead.UI\bin\Release\net10.0-windows\*') (Join-Path $work 'app') -Recurse -Force
    Copy-Item (Join-Path $repo 'src\PlayStead.Updater\bin\Release\net10.0\*') (Join-Path $work 'updater') -Recurse -Force
}
$app = Join-Path $work 'app'
$updater = Join-Path $work 'updater'
if (-not (Test-Path (Join-Path $app 'PlayStead.UI.exe'))) { throw 'PlayStead.UI.exe is missing from the publish output.' }
if (-not (Test-Path (Join-Path $updater 'PlayStead.Updater.exe'))) { throw 'PlayStead.Updater.exe is missing from the publish output.' }
# The updater is kept as a complete detached payload; the UI publish remains
# authoritative for its own WPF/runtime assemblies.
<#
Get-ChildItem $updater -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($updater.Length).TrimStart('\','/')
    $destination = Join-Path $app $relative
    if (-not (Test-Path $destination)) {
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item $_.FullName $destination -Force
    }
}
#>
$updaterHost = Join-Path $app 'UpdaterHost'
New-Item -ItemType Directory -Path $updaterHost -Force | Out-Null
Copy-Item (Join-Path $updater '*') $updaterHost -Recurse -Force
# Remove stale root-level updater artifacts left by older local builds.  The
# authoritative payload is now only under UpdaterHost.
foreach ($name in @('PlayStead.Updater.exe','PlayStead.Updater.dll','PlayStead.Updater.deps.json','PlayStead.Updater.runtimeconfig.json','PlayStead.Updater.pdb')) {
    $stale = Join-Path $app $name
    if (Test-Path $stale) { Remove-Item $stale -Force }
}
Set-Content (Join-Path $app 'distribution-channel.txt') 'GitHub' -NoNewline
Get-ChildItem $app -Recurse -File -Filter '*.pdb' | Remove-Item -Force
if (Test-Path (Join-Path $app 'runtimes')) {
    Get-ChildItem (Join-Path $app 'runtimes') -Directory | Where-Object Name -ne 'win-x64' | Remove-Item -Recurse -Force
}
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $app '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content $sha "$hash  $(Split-Path $zip -Leaf)"
$manifestObject = [ordered]@{ channel = $Channel; version = $Version; publishedAtUtc = [DateTime]::UtcNow.ToString('O'); packageUrl = $packageUrlForManifest; sha256 = $hash; releaseNotesUrl = $releaseNotesUrlForManifest }
$manifestObject | ConvertTo-Json | Set-Content $manifest
Remove-Item $work -Recurse -Force
Write-Host "GitHub artifact: $zip"
Write-Host "SHA-256: $sha"
Write-Host "Manifest: $manifest"
