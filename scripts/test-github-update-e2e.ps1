[CmdletBinding()]
param(
    [string]$Version1 = '0.4.3-alpha1',
    [string]$Version2 = '0.4.3-alpha2',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repo 'artifacts\github'
$sandbox = Join-Path $env:TEMP ('PlayStead-GitHub-Update-E2E-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $sandbox 'install'
$updatesRoot = Join-Path $sandbox 'updates'
$logs = Join-Path $sandbox 'logs'
$results = [ordered]@{}
$failed = $false

function Set-Result([string]$Name, [bool]$Pass) {
    $results[$Name] = if ($Pass) { 'PASS' } else { 'FAIL' }
    Write-Output ("{0}={1}" -f $Name, $results[$Name])
}

function Assert-Stage([string]$Name, [scriptblock]$Action) {
    try { & $Action; Set-Result $Name $true }
    catch { Write-Error ("[{0}] {1}" -f $Name, $_.Exception.Message); Set-Result $Name $false; throw }
}

New-Item -ItemType Directory -Path $install,$updatesRoot,$logs -Force | Out-Null
try {
    $old = Get-Process -Name 'PlayStead.UI','PlayStead.Updater' -ErrorAction SilentlyContinue
    if ($old) { throw 'An existing PlayStead UI/updater process is active.' }

    if (-not $SkipPublish) {
        & (Join-Path $PSScriptRoot 'publish-github.ps1') -Version $Version1
        & (Join-Path $PSScriptRoot 'publish-github.ps1') -Version $Version2
    }

    $zip1 = Join-Path $artifactRoot "PlayStead-$Version1-win-x64.zip"
    $zip2 = Join-Path $artifactRoot "PlayStead-$Version2-win-x64.zip"
    Assert-Stage 'PACKAGE_ALPHA1' { if (-not (Test-Path $zip1)) { throw "Missing $zip1" } }
    Assert-Stage 'PACKAGE_ALPHA2' { if (-not (Test-Path $zip2)) { throw "Missing $zip2" } }

    Expand-Archive -Path $zip1 -DestinationPath $install -Force
    $ui = Join-Path $install 'PlayStead.UI.exe'
    $updaterHost = Join-Path $install 'UpdaterHost'
    Assert-Stage 'PACKAGE_ALPHA1_CONTENT' {
        if (-not (Test-Path $ui)) { throw 'PlayStead.UI.exe is missing.' }
        if (-not (Test-Path (Join-Path $updaterHost 'PlayStead.Updater.exe'))) { throw 'UpdaterHost payload is missing.' }
        if (Test-Path (Join-Path $install 'PlayStead.Updater.exe')) { throw 'Updater is still present at package root.' }
        if ((Get-Item $ui).VersionInfo.ProductVersion -notlike "$Version1*") { throw 'Alpha1 ProductVersion mismatch.' }
    }

    $versionDir = Join-Path $updatesRoot $Version2
    $package = Join-Path $versionDir 'package.zip'
    $runner = Join-Path $versionDir 'updater-runner'
    New-Item -ItemType Directory -Path $versionDir,$runner -Force | Out-Null
    Copy-Item $zip2 $package
    Copy-Item (Join-Path $updaterHost '*') $runner -Recurse -Force
    $runnerExe = Join-Path $runner 'PlayStead.Updater.exe'
    Assert-Stage 'DOWNLOAD_OR_FIXTURE' { if (-not (Test-Path $package)) { throw 'Package fixture missing.' } }
    Assert-Stage 'SHA' {
        $expected = (Get-FileHash $zip2 -Algorithm SHA256).Hash
        $actual = (Get-FileHash $package -Algorithm SHA256).Hash
        if ($expected -ne $actual) { throw 'SHA-256 mismatch.' }
    }
    Assert-Stage 'DETACHED_RUNNER' {
        if (-not (Test-Path $runnerExe)) { throw 'Detached updater is missing.' }
        $targetFull = [IO.Path]::GetFullPath($install).TrimEnd('\') + '\'
        $runnerFull = [IO.Path]::GetFullPath($runnerExe).TrimEnd('\') + '\'
        if ($runnerFull.StartsWith($targetFull, [StringComparison]::OrdinalIgnoreCase)) { throw 'Runner is under target.' }
    }

    $source = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 10' -PassThru -WindowStyle Hidden
    $relaunch = Join-Path $install 'UpdaterHost\PlayStead.Updater.exe'
    $arguments = @('--pid', $source.Id, '--package', $package, '--updates-root', $updatesRoot, '--target-dir', $install, '--exe', $relaunch, '--version', $Version2)
    $stdout = Join-Path $logs 'updater.stdout.log'; $stderr = Join-Path $logs 'updater.stderr.log'
    $process = Start-Process -FilePath $runnerExe -ArgumentList $arguments -WorkingDirectory $runner -PassThru -Wait -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    Assert-Stage 'APPLY' {
        if ($process.ExitCode -ne 0) { throw "Updater exit code $($process.ExitCode)." }
        $log = Get-Content (Join-Path $updatesRoot 'updater.log') -Raw
        if ($log -notmatch 'ApplyCompleted') { throw 'ApplyCompleted is missing.' }
        if ($log -match 'ValidationFailed') { throw 'ValidationFailed found in updater log.' }
    }
    Assert-Stage 'RELAUNCH' { if (-not $process) { throw 'Updater process did not complete.' } }
    Assert-Stage 'VERSION_ALPHA2' { if ((Get-Item $ui).VersionInfo.ProductVersion -notlike "$Version2*") { throw 'Alpha2 ProductVersion mismatch.' } }

    # A corrupt package exercises the updater failure/rollback path without touching
    # a real installation. The alpha1 executable must remain unchanged.
    $rollbackInstall = Join-Path $sandbox 'rollback-install'
    Expand-Archive -Path $zip1 -DestinationPath $rollbackInstall -Force
    $badDir = Join-Path $updatesRoot 'rollback'; New-Item -ItemType Directory -Path $badDir -Force | Out-Null
    $badPackage = Join-Path $badDir 'package.zip'; Set-Content $badPackage 'not a zip' -NoNewline
    $badRunner = Join-Path $badDir 'updater-runner'; New-Item -ItemType Directory -Path $badRunner -Force | Out-Null; Copy-Item (Join-Path $rollbackInstall 'UpdaterHost\*') $badRunner -Recurse -Force
    $badExe = Join-Path $rollbackInstall 'UpdaterHost\PlayStead.Updater.exe'
    $badArgs = @('--pid', $source.Id, '--package', $badPackage, '--updates-root', $updatesRoot, '--target-dir', $rollbackInstall, '--exe', $badExe, '--version', $Version2)
    $bad = Start-Process -FilePath (Join-Path $badRunner 'PlayStead.Updater.exe') -ArgumentList $badArgs -WorkingDirectory $badRunner -PassThru -Wait
    Assert-Stage 'ROLLBACK_TEST' { if ($bad.ExitCode -eq 0) { throw 'Corrupt package unexpectedly succeeded.' }; if ((Get-Item (Join-Path $rollbackInstall 'PlayStead.UI.exe')).VersionInfo.ProductVersion -notlike "$Version1*") { throw 'Alpha1 was not preserved.' } }
}
catch {
    $failed = $true
    Write-Error $_
}
finally {
    if ($source -and -not $source.HasExited) { Stop-Process -Id $source.Id -Force -ErrorAction SilentlyContinue }
    Write-Output "E2E_SANDBOX=$sandbox"
    $success = -not $failed -and $results.Count -gt 0 -and ($results.Values | Where-Object { $_ -eq 'FAIL' }).Count -eq 0
    Set-Result 'E2E_UPDATE' $success
}
