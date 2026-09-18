param(
    [switch]$Deploy,
    [string]$ValheimPath = $env:VALHEIM_PATH,
    [string]$ProfilePath = $env:VALHEIM_PROFILE_PATH
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ValheimPath) -or [string]::IsNullOrWhiteSpace($ProfilePath)) {
    throw 'Set VALHEIM_PATH and VALHEIM_PROFILE_PATH, or pass -ValheimPath and -ProfilePath.'
}
$projectPath = Join-Path $PSScriptRoot 'FreshDedicatedStorage\SmallStorageChest.csproj'
& dotnet build $projectPath --configuration Release --nologo "-p:ValheimPath=$ValheimPath" "-p:ProfilePath=$ProfilePath"
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Nothing was deployed.' }
if ($Deploy) {
    if (Get-Process -Name valheim -ErrorAction SilentlyContinue) {
        throw 'Close Valheim before deploying. The build is ready; rerun with -Deploy after closing the game.'
    }
    if (!(Test-Path -LiteralPath (Join-Path $ProfilePath 'BepInEx\core\BepInEx.dll'))) {
        throw 'The selected profile does not contain BepInEx.'
    }
    $destinationPath = Join-Path $ProfilePath 'BepInEx\plugins\Mike-SmallStorageChest'
    New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    $compiledPath = Join-Path $PSScriptRoot 'FreshDedicatedStorage\bin\Release\netstandard2.1\SmallStorageChest.dll'
    $installedPath = Join-Path $destinationPath 'SmallStorageChest.dll'
    if (Test-Path -LiteralPath $installedPath) {
        Copy-Item -LiteralPath $installedPath -Destination "$installedPath.previous" -Force
    }
    Copy-Item -LiteralPath $compiledPath -Destination $installedPath -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FreshDedicatedStorage\Assets\QUATERNIUS-LICENSE.txt') -Destination $destinationPath -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FreshDedicatedStorage\Assets\SOURCE.md') -Destination (Join-Path $destinationPath 'ASSET-CREDITS.md') -Force
    if ((Get-FileHash -LiteralPath $compiledPath).Hash -ne (Get-FileHash -LiteralPath $installedPath).Hash) {
        throw 'Deployed DLL hash does not match the build.'
    }
    Write-Output "Installed and verified: $installedPath"
}

