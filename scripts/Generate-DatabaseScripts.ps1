[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'MarksBaseballCards.slnx'))) { throw 'Run from this repository.' }
Push-Location $repoRoot
try {
    dotnet tool restore --tool-manifest dotnet-tools.json
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet build src/Api/MarksBaseballCards.Api.csproj -c Release --warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet tool run dotnet-ef migrations script --idempotent --project src/Api --configuration Release --no-build --output deploy/sql/card-photo-onboarding.sql
    if ($LASTEXITCODE -ne 0) { throw 'SQL generation failed.' }
    $sqlOutputPath = Join-Path $repoRoot 'deploy/sql/card-photo-onboarding.sql'
    [System.IO.File]::WriteAllText($sqlOutputPath, [System.IO.File]::ReadAllText($sqlOutputPath).TrimEnd() + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Output 'Generated deploy/sql/card-photo-onboarding.sql. Review it; this script did not connect to or alter a database.'
} finally { Pop-Location }
