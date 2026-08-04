# Codex Scripts

Run these commands from the repository root using PowerShell.

---

## Setup

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File ./.codex/setup.ps1
```

This only verifies the toolchain (it checks the `.env` encoding, then delegates
to `task doctor`). There is no environment to initialize: Task loads
`.env.local` then `.env` on every invocation.

---

## Standard Repository Commands

```powershell
task verify      # build + unit tests — everything required before merging
task build       # fast inner loop
task test
task clean
task doctor
task pack        # build the NuGet package locally
task --list      # everything available
```

Avoid directly invoking `dotnet build`, `dotnet test`, or `script/*.ps1` unless
you are debugging the build system itself. `Taskfile.yml` is the authoritative
task graph.

---

## Codex / Agent Shell Behavior

Some AI agent environments launch each command in a fresh PowerShell process.
That is fine here — every task works in a fresh process with no preamble:

```powershell
task verify
```

Nothing needs to be sourced first, and no environment variables, aliases, or
PowerShell functions need to persist between commands.
