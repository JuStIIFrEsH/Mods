param(
    [switch]$Deploy,
    [string]$ValheimPath = $env:VALHEIM_PATH,
    [string]$ProfilePath = $env:VALHEIM_PROFILE_PATH
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ValheimPath) -or [string]::IsNullOrWhiteSpace($ProfilePath)) {
    throw 'Set VALHEIM_PATH and VALHEIM_PROFILE_PATH, or pass -ValheimPath and -ProfilePath.'
}
$project = Join-Path $PSScriptRoot 'FreshCommands\ConsoleCommands.csproj'
& dotnet build $project --configuration Release --nologo "-p:ValheimPath=$ValheimPath" "-p:ProfilePath=$ProfilePath"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$dll = Join-Path $PSScriptRoot 'FreshCommands\bin\Release\netstandard2.1\FreshCommands.dll'
$manifestPath = Join-Path $PSScriptRoot 'FreshCommands\manifest.json'
$version = (Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json).version_number
if ([string]::IsNullOrWhiteSpace($version)) { throw 'No FreshCommands version was found in manifest.json.' }
$production = Join-Path $PSScriptRoot 'production\FreshCommands'
$package = Join-Path ([System.IO.Path]::GetTempPath()) "FreshCommands-$version"
$zip = Join-Path $production "FreshCommands-$version.zip"
New-Item -ItemType Directory -Path $production -Force | Out-Null
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Recurse -Force }
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FreshCommands\README.md') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FreshCommands\CHANGELOG.md') -Destination $package -Force
Copy-Item -LiteralPath $manifestPath -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FreshCommands\icon.png') -Destination $package -Force

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip
Remove-Item -LiteralPath $package -Recurse -Force
Write-Output "Packaged: $zip"

if ($Deploy) {
    if (Get-Process -Name valheim -ErrorAction SilentlyContinue) {
        throw 'Close Valheim before deploying. The package is already built.'
    }
    $destination = Join-Path $ProfilePath 'BepInEx\plugins\FreshCommands'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination $destination -Force
    Write-Output "Installed: $destination"
}
