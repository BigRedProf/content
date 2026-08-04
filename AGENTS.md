# Agent Instructions

This repository follows the BigRedProf development environment conventions. It
is the single source of shared instructions for agents and contributors;
`CLAUDE.md` imports this file via `@AGENTS.md`.

---

## Authoritative Coding Standards

All formatting, organization, naming, nullability, defensive programming, and
structural code-style rules are defined in `CODING_GUIDELINES.md`, which is the
authoritative source of truth. If anything here conflicts with it, follow
`CODING_GUIDELINES.md`.

---

## Standard Commands

This repository is driven by [Task](https://taskfile.dev). Task is the
orchestration layer and loads the layered environment (`.env.local` then `.env`)
on every invocation, so no shell setup is needed — commands work in a fresh
process for humans and agents alike.

```powershell
task build      # fast inner loop (restore once, then build)
task test       # unit tests, no rebuild
task verify     # everything required before merging — the success criterion
task clean
task doctor     # toolchain/version diagnostics
task pack       # build the NuGet package locally
```

List everything with `task --list`.

`verify` is the canonical success criterion. It is fast by design (build +
unit tests).

Content specifics:

- The build **target** is `src/Content.sln`. Note the solution lives under
  `src/`, not at the repository root.
- Unit tests are real: `task test` runs `src/Core.Test`. It sits under `src/`
  rather than a top-level `tests/` directory, which is a known deviation from
  `REPO_CONVENTIONS.md`.
- There is **no container image** here — this repository ships a library, not a
  service, so there is no `image` or `publish` task.
- `BigRedProf.Content.Core` is published to GitHub Packages by CI on a push to
  `main`. `task pack` only builds the package locally and deliberately cannot
  push.
- There is no `.config/dotnet-tools.json`, so `restore` does not run
  `dotnet tool restore`. Add both together if a local tool is ever introduced.

---

## How It Fits Together

- **`Taskfile.yml`** — the authoritative task graph. Simple verbs (restore,
  build, test, verify, clean) are defined directly here so the graph restores
  once, builds once, and tests without rebuilding.
- **`script/*.ps1`** — only genuinely complex, multi-step behavior (`doctor`).
  Task invokes these in their own process.
- **`.env`** (committed) / **`.env.local`** (gitignored, wins) — per-developer
  environment preferences such as configuration. The authoritative build
  **target is in `Taskfile.yml`**, not in `.env`, so everyone verifies the same
  projects.

Do not reintroduce a general-purpose PowerShell orchestration layer; Task owns
orchestration.

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

The canonical/shared version of common PowerShell utilities lives in the
BigRedProf foundation repository:

```text
foundation/templates/dotnet/script/common.ps1
```

Each repository contains its own versioned copy under `script/common.ps1` so
repositories can evolve independently.
