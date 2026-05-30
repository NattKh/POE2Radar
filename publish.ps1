# Builds a self-contained, single-file Windows x64 release of the overlay and zips it.
# Usage:  ./publish.ps1            (version defaults to 'dev')
#         ./publish.ps1 v0.1.0
param([string]$Version = "dev")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

dotnet publish "$root/src/POE2Radar.Overlay/POE2Radar.Overlay.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$root/publish"

Copy-Item "$root/README.md", "$root/LICENSE" "$root/publish/" -Force
$zip = "$root/POE2Radar-$Version-win-x64.zip"
Compress-Archive -Path "$root/publish/*" -DestinationPath $zip -Force
Write-Host "Built: $zip"
