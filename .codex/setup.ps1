$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$setenvPath = Join-Path $repoRoot 'script\setenv.ps1'

Push-Location $repoRoot

try
{
	Write-Host ""
	Write-Host "[setup] initializing BigRedProf Codex environment"

	if (-not (Test-Path -LiteralPath $setenvPath -PathType Leaf))
	{
		throw "Could not find BigRedProf environment script: $setenvPath"
	}

	. $setenvPath

	Write-Host ""
	Write-Host "[setup] verifying prof command"

	if (-not (Get-Command prof -ErrorAction SilentlyContinue))
	{
		throw "The prof command was not loaded by setenv.ps1."
	}

	Write-Host "[setup] prof command available"

	Write-Host ""
	Write-Host "[setup] example commands:"
	Write-Host "  . .\script\setenv.ps1; prof build"
	Write-Host "  . .\script\setenv.ps1; prof test"

	Write-Host ""
	Write-Host "[setup] OK: Codex setup completed."
}
finally
{
	Pop-Location
}