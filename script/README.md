# Script Directory

This directory contains only the genuinely complex, multi-step scripts for this
repository. The simple verbs (`restore`, `build`, `test`, `verify`, `clean`,
`pack`) are defined directly in `Taskfile.yml` — Task is the orchestration
layer, and the repository does not maintain a general-purpose PowerShell build
framework.

## Scripts

| Script       | Invoked by    | Purpose                                                                    |
| ------------ | ------------- | -------------------------------------------------------------------------- |
| `common.ps1` | (dot-sourced) | Shared helpers: `Write-Step`, `Invoke-Checked`, `Test-CommandExists`, `Get-RepoRoot`, `Test-DotEnvEncoding` |
| `doctor.ps1` | `task doctor` | Toolchain diagnostics; checks Task, .NET, `.env` encoding                   |

That is the whole list, and deliberately so. This repository ships a **library**,
not a service: there is no Dockerfile, no container image, and therefore no
`image` or `publish` script. Sibling repositories (`stories`, `digihouse`) have
those because they deploy services.

## Publishing

`BigRedProf.Content.Core` is pushed to GitHub Packages by
`.github/workflows/dotnet.yml` on a push to `main`. `task pack` builds the
package locally into `artifacts/packages` and deliberately cannot push, so
nothing local can release a package by accident.

## Conventions

- These scripts are invoked by Task in their own `pwsh -File` process, so a
  thrown error propagates as a non-zero exit code. Do not chain them in-process.
- Paths are resolved from `Get-RepoRoot`, never hard-coded.
- Environment comes from the `.env` files, which Task loads before invoking a
  script. Scripts do not load `.env` themselves.

## The .env files MUST be UTF-8

Task's dotenv parser reads UTF-8 only, and on a malformed file its error message
**echoes the file's contents** — so a UTF-16 `.env.local` would leak anything it
holds into the console and any captured log.

This is easy to hit by accident: **Windows PowerShell 5.1's `>` and `>>`
redirection writes UTF-16**. Use pwsh 7, or write the file explicitly:

```powershell
[IO.File]::WriteAllText('.env.local', "KEY=value`n", (New-Object Text.UTF8Encoding $false))
```

Because Task parses these files at startup, it fails *before* it can run any
task — so `task doctor` cannot diagnose this. Run doctor directly instead:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File script/doctor.ps1
```

That is the escape hatch for a repository whose `.env` files stop Task running
at all. `Test-DotEnvEncoding` in `common.ps1` implements the check.

## Common Utilities

`common.ps1` is intentionally versioned per-repository. The canonical source
lives at:

```text
foundation/templates/dotnet/script/common.ps1
```
