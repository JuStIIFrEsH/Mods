param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'FreshDedicatedStorage\FreshStorage.csproj'
$version = ([xml](Get-Content -Raw -LiteralPath $project)).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { throw 'No project version was found.' }

$packageSource = Join-Path $root 'FreshDedicatedStorage'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) "FreshDedicatedStorage-$version"
$production = Join-Path $root 'production\FreshDedicatedStorage'
$zip = Join-Path $production "FreshDedicatedStorage-$version.zip"
$dll = Join-Path $root "FreshDedicatedStorage\bin\$Configuration\netstandard2.1\FreshStorage.dll"

& dotnet build $project --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed; package was not created.' }
if (!(Test-Path -LiteralPath $dll)) { throw "Built DLL is missing: $dll" }

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path $production | Out-Null
Copy-Item -LiteralPath (Join-Path $packageSource 'manifest.json'), (Join-Path $packageSource 'README.md'), (Join-Path $packageSource 'CHANGELOG.md'), (Join-Path $packageSource 'ASSET-CREDITS.md'), (Join-Path $packageSource 'LICENSE') -Destination $stage

$manifest = Get-Content -Raw -LiteralPath (Join-Path $stage 'manifest.json') | ConvertFrom-Json
if ($manifest.version_number -ne $version) { throw "Manifest version $($manifest.version_number) does not match project version $version." }

& (Join-Path $root 'tools\create_thunderstore_icon.ps1') -OutputPath (Join-Path $stage 'icon.png')
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile((Join-Path $stage 'icon.png'))
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256) { throw 'icon.png must be exactly 256x256.' }
}
finally { $icon.Dispose() }

$pluginDir = Join-Path $stage 'BepInEx\plugins\JuStIIFrEsH-FreshDedicatedStorage'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $pluginDir 'FreshStorage.dll')

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $stage | ForEach-Object FullName) -DestinationPath $zip -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries.FullName)
    foreach ($required in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png', 'BepInEx/plugins/JuStIIFrEsH-FreshDedicatedStorage/FreshStorage.dll')) {
        if ($entries -notcontains $required) { throw "Package is missing required entry: $required" }
    }
    if ($entries | Where-Object { $_ -like "FreshDedicatedStorage-$version/*" }) { throw 'ZIP must not contain a wrapping directory.' }
}
finally { $archive.Dispose() }

Remove-Item -LiteralPath $stage -Recurse -Force
Write-Output "Package validated: $zip"
Get-ChildItem -LiteralPath $zip | Select-Object FullName, Length, LastWriteTime
