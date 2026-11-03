# BlinkCameraServiceHandler

A Windows console application for logging into a personal [Blink](https://blinkforhome.com/)
home-security account and running commands against it: login (including Blink's
two-factor codes), logout, client-trust PIN verification, and fetching your account's
"home screen" (networks, sync modules, cameras, doorbells).

- **Using it:** see [docs/USER_GUIDE.md](docs/USER_GUIDE.md) for setup, commands, and
  troubleshooting.
- **Installing it:** see [scripts/install.ps1](scripts/install.ps1) /
  [scripts/uninstall.ps1](scripts/uninstall.ps1).
- **How it's built / how to rebuild it from scratch:** see [docs/DEVELOPER.md](docs/DEVELOPER.md).

## Quick start

```powershell
# Build and install to %LOCALAPPDATA%\BlinkCameraServiceHandler, add it to your PATH
.\scripts\install.ps1

# Open a new terminal, then:
blink
```

Or run straight from source with the .NET 8 SDK:

```powershell
dotnet run --project BlinkCameraCall
```
