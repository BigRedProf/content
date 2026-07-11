# Agent Instructions

This repository follows the BigRedProf development environment conventions.

---

## Authoritative Coding Standards

All formatting, organization, naming, nullability, defensive programming,
and structural code-style rules are defined in:

```text
CODING_GUIDELINES.md
```

That document is the authoritative source of truth for repository code style.

If there is any conflict between this file and `CODING_GUIDELINES.md`,
follow `CODING_GUIDELINES.md`.

Agents and contributors are expected to follow those conventions consistently.

---

## Environment Initialization

Before running repository commands in PowerShell, initialize the environment:

```powershell
. .\script\setenv.ps1
```

This loads:

* the BigRedProf `prof` command
* repository environment variables
* repository aliases/functions
* standard .NET settings

---

## Standard Commands

Always prefer the `prof` command for repository tasks:

```powershell
prof build
prof test
prof clean
```

Shortcut aliases are also available:

```powershell
p build
p test
```

Do not call the following directly unless debugging the build system itself:

```powershell
dotnet build
dotnet test
script\build.ps1
script\test.ps1
```

The `prof` command is the standard BigRedProf task runner and dispatches to scripts under `script/`.

---

## Agent Shell Behavior

Some AI agent environments launch each command in a fresh PowerShell process.

In those environments, initialize the environment and execute the command in the same invocation:

```powershell
. .\script\setenv.ps1; prof build
```

Example:

```powershell
. .\script\setenv.ps1; prof test
```

Do not assume environment variables, aliases, or PowerShell functions persist between separate agent commands.

---

## Build Philosophy

BigRedProf repositories intentionally prefer:

* small scripts
* deterministic behavior
* explicit conventions
* repository-local tooling
* minimal hidden magic

Avoid introducing:

* implicit machine-wide dependencies
* hidden environment mutation
* auto-install behavior
* opaque orchestration layers

---

## Notes

The canonical/shared version of common PowerShell utilities lives in the BigRedProf foundation repository:

```text
foundation/script/common.ps1
```

Each repository contains its own versioned copy under:

```text
script/common.ps1
```

This is intentional so repositories can evolve independently and safely.
