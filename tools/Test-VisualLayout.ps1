# Copyright (c) 2026 EIDO AUTOMATION, S.L.U.
# Test-VisualLayout.ps1
# Builds and runs offline UI checks; writes renders under artifacts/validation.
# Developed by EIDO Automation
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('Checks', 'Design', 'Preview')][string]$Mode = 'Checks'
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$output = Join-Path $PSScriptRoot "bin\x64\$Configuration"
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
& $msbuild (Join-Path $PSScriptRoot 'VisualValidation.csproj') /restore /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /warnaserror /nologo /v:q
if ($LASTEXITCODE -ne 0) { throw 'Visual probe compilation failed.' }
switch ($Mode) {
    'Preview' { & "$output\VisualValidation.exe" --preview }
    'Design' { & "$output\VisualValidation.exe" --design (Join-Path $workspace 'artifacts\validation\redesign') }
    default { & "$output\VisualValidation.exe" (Join-Path $workspace 'artifacts\validation\redesign-shell') }
}
if ($LASTEXITCODE -ne 0) { throw 'Visual probe failed.' }
