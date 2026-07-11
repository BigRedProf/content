# Codex Scripts

Run these commands from the repository root using PowerShell.

---

## Environment Initialization

Initialize the BigRedProf development environment:

```powershell
. .\script\setenv.ps1
```

This loads:
- the `prof` command
- repository aliases/functions
- repository environment variables
- standard .NET settings

---

## Standard Repository Commands

Prefer the BigRedProf `prof` command for all normal repository tasks:

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

Avoid directly invoking:
- `dotnet build`
- `dotnet test`
- `script/*.ps1`

unless debugging the build system itself.

---

## Codex / Agent Shell Behavior

Some AI agent environments launch each command in a fresh PowerShell process.

In those environments, initialize the environment and run the command in the same invocation:

```powershell
. .\script\setenv.ps1; prof build
```

Example:

```powershell
. .\script\setenv.ps1; prof test
```

Do not assume environment variables, aliases, or PowerShell functions persist between separate agent commands.

