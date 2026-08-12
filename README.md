# SocialBlocker — Phase 1-2 scaffold

This implements the foundation from the development plan: block-list config
storage, the hosts-file blocking engine, and a background Windows Service that
persists a block session across reboots and re-applies it every 10 seconds so
it self-heals if something reverts the hosts file mid-session.

Later phases (DNS sinkhole, firewall rules + process watchdog, the tray UI,
tamper-resistance) plug into this same `BlockConfig` / `ConfigStore` / service
loop — the hook points are marked with comments in `Worker.cs`.

## Projects

- **`src/SocialBlocker.Core`** — the config model, JSON persistence (with a
  best-effort ACL lockdown), and the hosts-file engine. The two functions
  worth reading closely are `HostsFileManager.BuildBlockedLines` and
  `StripManagedBlock` — pure functions, no file I/O, everything else is thin
  plumbing around them.
- **`src/SocialBlocker.Service`** — the Windows Service (.NET Worker Service
  host) that enforces whatever session is currently in the config.
- **`src/SocialBlocker.Cli`** — a command-line control tool. Stands in for the
  Phase 5 tray UI so you have a way to actually drive this end-to-end today.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 to install and run the service for real. The non-Windows-specific
  parts also build and run on macOS/Linux for convenience while developing —
  hosts-file and ACL behavior only activate when `OperatingSystem.IsWindows()`
  is true.

## Build

From the `SocialBlocker` folder:

```
dotnet build src/SocialBlocker.Core
dotnet build src/SocialBlocker.Service
dotnet build src/SocialBlocker.Cli
```

The first build needs internet access once, to restore two small NuGet
packages: `Microsoft.Extensions.Hosting.WindowsServices` and
`System.IO.FileSystem.AccessControl`.

## Try it without installing the service (fastest way to see it work)

1. Run the self-test — confirms the hosts-file logic is correct without
   touching any real file:
   ```
   dotnet run --project src/SocialBlocker.Cli -- selftest
   ```
2. Run the service directly in a console window instead of installing it
   (open the terminal **as Administrator** — writing to the hosts file needs
   elevation):
   ```
   dotnet run --project src/SocialBlocker.Service
   ```
   Leave that running — it's now polling every 10 seconds.
3. In a second terminal, start a 1-minute block session:
   ```
   dotnet run --project src/SocialBlocker.Cli -- start 1
   ```
4. Open `C:\Windows\System32\drivers\etc\hosts` in Notepad — a `SocialBlocker`
   block appears within ~10 seconds, and disappears on its own after 1 minute.
5. Check remaining time any time:
   ```
   dotnet run --project src/SocialBlocker.Cli -- status
   ```

## Install as an actual Windows Service (survives logoff/reboot)

Run as Administrator:

```
dotnet publish src/SocialBlocker.Service -c Release -o out/service
sc.exe create SocialBlockerService binPath= "C:\full\path\to\out\service\SocialBlocker.Service.exe"
sc.exe config SocialBlockerService start= auto
sc.exe start SocialBlockerService
```

(Note the required space after `binPath=` and `start=` — `sc.exe` is picky
about that.)

To remove it later:

```
sc.exe stop SocialBlockerService
sc.exe delete SocialBlockerService
```

## What's deliberately not here yet

Per the phased plan: DNS sinkhole (Phase 3), firewall rules + process
watchdog (Phase 4), the tray UI (Phase 5), and tamper-resistance like the
early-exit passphrase (Phase 6) — the CLI's `stop` command currently refuses
to lift an active session early, on purpose, matching that design.

## A note on how this was built

This code was written and reviewed carefully, but the sandbox that produced
it has no .NET SDK and no internet access, so it could not actually be
compiled or run there. Start with `selftest` — if anything doesn't build
cleanly, it's most likely a small typo rather than a design problem, and
`HostsFileManager.BuildBlockedLines` / `StripManagedBlock` are the two
functions worth double-checking first since everything else is straightforward
plumbing around them.
