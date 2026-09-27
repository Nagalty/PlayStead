[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo "artifacts\store\$Version-$RuntimeIdentifier"
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (-not $SkipBuild) {
    dotnet publish (Join-Path $repo 'src\PlayStead.UI\PlayStead.UI.csproj') -c Release -r $RuntimeIdentifier --self-contained false -p:PlaySteadDistributionChannel=MicrosoftStore -p:PlaySteadProductVersion=$Version -p:Version=$Version -p:BuildUpdater=false -o $output
} else {
    Copy-Item (Join-Path $repo 'src\PlayStead.UI\bin\Release\net10.0-windows\*') $output -Recurse -Force
}
if (Test-Path (Join-Path $output 'PlayStead.Updater.exe')) { Remove-Item (Join-Path $output 'PlayStead.Updater.exe') -Force }
Get-ChildItem $output -Recurse -File -Filter '*.pdb' | Remove-Item -Force
if (Test-Path (Join-Path $output 'runtimes')) {
    Get-ChildItem (Join-Path $output 'runtimes') -Directory | Where-Object Name -ne 'win-x64' | Remove-Item -Recurse -Force
}
Set-Content (Join-Path $output 'distribution-channel.txt') 'MicrosoftStore' -NoNewline
Write-Host "Microsoft Store preparation output: $output"
Write-Host 'MSIX/Package Identity/ProductId are not configured in this repository.'
