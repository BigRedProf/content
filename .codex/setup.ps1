$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

<#
Minimal Codex environment check.

There is nothing to "enter" or source: Task loads the repository environment
(.env.local then .env) on every invocation, so `task build` works in a fresh
process. This script only verifies the toolchain is present by delegating to the
repository's own diagnostics.

(It previously dot-sourced script/setenv.ps1 and looked for a `prof` command --
neither of which has ever existed in this repository, so it always threw.)
#>

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

Push-Location $repoRoot

try
{
	Write-Host ""
	Write-Host "[setup] verifying the content toolchain"

	if (-not (Get-Command task -ErrorAction SilentlyContinue))
	{
		throw "Task is not on PATH. Install it with: choco install go-task"
	}

	# Check the .env files BEFORE handing off to Task. Task parses them at
	# startup and, on a malformed (e.g. UTF-16) file, prints the file's CONTENTS
	# in its error. This runs outside Task, so it is the only place that can
	# catch it first.
	. (Join-Path $repoRoot "script\common.ps1")

	if (-not (Test-DotEnvEncoding -RepoRoot $repoRoot))
	{
		throw "Fix the .env file encoding above before running Task."
	}

	& task doctor

	if ($LASTEXITCODE -ne 0)
	{
		throw "task doctor reported problems (exit code ${LASTEXITCODE})."
	}

	Write-Host ""
	Write-Host "[setup] example commands:"
	Write-Host "  task verify"
	Write-Host "  task build"
	Write-Host "  task --list"

	Write-Host ""
	Write-Host "[setup] OK: Codex setup completed."
}
finally
{
	Pop-Location
}
