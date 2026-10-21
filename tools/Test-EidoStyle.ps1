# Copyright (c) 2026 EIDO AUTOMATION, S.L.U.
# Test-EidoStyle.ps1
# Verifies owned source headers and formatting, then builds all projects with warnings as errors.
# Developed by EIDO Automation
param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }

Push-Location $workspace
try {
    $sources = Get-ChildItem -LiteralPath $workspace -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts|\.git|\.vs)\\' -and $_.Name -notlike '*.Designer.cs' }
    foreach ($source in $sources) {
        $bytes = [IO.File]::ReadAllBytes($source.FullName)
        $text = [IO.File]::ReadAllText($source.FullName)
        if ($bytes.Length -lt 3 -or $bytes[0] -ne 239 -or $bytes[1] -ne 187 -or $bytes[2] -ne 191) {
            throw "Missing UTF-8 BOM: $($source.FullName)"
        }
        if ($text -match '(?<!\r)\n' -or -not $text.EndsWith("`r`n")) {
            throw "Expected CRLF and final newline: $($source.FullName)"
        }
        $header = ($text -split '\r\n' | Select-Object -First 16) -join "`r`n"
        if (-not $header.Contains('// ' + $source.Name) -or
            -not $header.Contains('// Developed by EIDO Automation') -or
            -not $header.Contains('// ' + ('=' * 73))) {
            throw "Missing EIDO source header: $($source.FullName)"
        }
    }

    dotnet format EidoTiaWorkbench.slnx whitespace --verify-no-changes --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace verification failed.' }
    foreach ($configuration in @('Debug', 'Release')) {
        & $msbuild EidoTiaWorkbench.slnx /restore /t:Rebuild "/p:Configuration=$configuration" /p:Platform=x64 /warnaserror /nologo /v:minimal
        if ($LASTEXITCODE -ne 0) { throw "Style/compiler verification failed: $configuration" }
    }
    Write-Output "PASS EIDO formatting, headers and compiler/analyzer checks for $($sources.Count) owned C# files."
}
finally {
    Pop-Location
}
