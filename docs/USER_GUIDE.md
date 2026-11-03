# User Guide

BlinkCameraServiceHandler is a Windows console application for logging into a
personal [Blink](https://blinkforhome.com/) home-security account and running a
small set of commands against it: login (including two-factor codes), logout,
client-trust PIN verification, fetching your "home screen" (networks, sync
modules, cameras, doorbells), and printing a readable device map.

If you want to know *how* it works instead of how to use it, see
[DEVELOPER.md](DEVELOPER.md).

## Requirements

- Windows, with the [.NET 8 runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  installed (the .NET 8 Desktop or Runtime package is enough; you don't need the SDK
  just to run the published app).
- A Blink account (email + password).

## Installing

See the install script in [`scripts/install.ps1`](../scripts/install.ps1) and the
"Install / uninstall" section below. You can also just build and run it from source
with `dotnet run --project BlinkCameraCall` if you have the .NET 8 SDK.

## First run: `settings.json`

On first run the app creates (next to the executable) a `settings.json` holding your
credentials and a persisted device id:

```json
{
  "Email": "test@email.com",
  "Password": "PAssword",
  "BaseUrl": "https://rest-prod.immedia-semi.com",
  "HardwareId": ""
}
```

Open that file and replace `Email`/`Password` with your real Blink credentials (or
just type credentials directly on the `LOGIN` command every time - see below). Leave
`BaseUrl` and `HardwareId` alone:

- `HardwareId` is filled in automatically the first time you run `LOGIN` and then
  left untouched. It identifies this installation as the same "device" to Blink on
  every future login, which is what lets Blink skip asking for a two-factor code on
  most logins instead of every single one. **Don't delete or regenerate it** unless
  you want to be treated as a brand-new device again (which will re-trigger 2FA).
- Treat this file like a password: it is not encrypted, and it is excluded from git
  on purpose (`.gitignore`). Don't commit it or share it.

## Running it

Start the program (from the install folder, or via `dotnet run --project BlinkCameraCall`
from source) and you'll see:

```
Blink Camera Terminal
```

Type commands at the prompt, one per line, and press Enter.

## Commands

| Command | Syntax | What it does |
|---|---|---|
| Login (using settings.json) | `LOGIN` | Logs in with the `Email`/`Password` already in `settings.json`. |
| Login (explicit credentials) | `LOGIN -u<email> -p<password>` | Logs in with the given email/password instead of what's in `settings.json`. No space between `-u`/`-p` and the value. |
| Verify a code | `VERIFY -v<code>` | Enters a verification code. Does one of two things depending on what just happened (see "Two-factor logins" below): completes a pending two-factor login, or verifies an old-style client-trust PIN. |
| Get home screen info | `GET -ihome` | Fetches your account's networks, sync modules, cameras and doorbells. Requires being logged in first. |
| Show a device map | `MAP` | Fetches the same data as `GET -ihome` and prints it as a readable tree: each network, with its sync module, cameras and doorbells nested underneath, each showing online/armed/enabled status, battery, wifi signal and firmware version. Requires being logged in first. |
| Logout | `LOGOUT` | Logs out of the current session. |
| Help | `HELP` | Prints command help. |
| Quit | `QUIT` or `EXIT` | Exits the program. |

Notes on syntax:
- Options are written as `-<letter><value>` with **no space** between the letter and
  the value, e.g. `-utrevor@example.com`, not `-u trevor@example.com`.
- Commands are case-insensitive.

### Example session

```
Blink Camera Terminal
LOGIN -utrevor@example.com -pMyRealPassword
Login to the Blink Service successful.

GET -ihome
Command ran successfully.

MAP
Network: Home (armed)
  Sync Module: My Blink Sync Module  [online, fw 16.0.8, wifi 5]
  Camera: Front Door  [enabled, status done, battery ok, wifi 5]

LOGOUT
Successfully logged out.

EXIT
Goodbye
```

## Device map (`MAP`)

`MAP` groups everything under the network it belongs to. A network with no
cameras or doorbells (just a sync module, or nothing at all) shows
`(no cameras or doorbells)` instead of an empty line. If Blink ever returns a
device that doesn't match any network in the response, it's listed under a
trailing `Unassigned devices:` section rather than silently dropped.

Blink Mini cameras, sirens, chimes and wired doorbell buttons aren't modeled
in detail yet — if your account has any, they show up as a bare count at the
end, e.g. `Owls (Blink Mini cameras): 2 (not detailed)`, rather than being
listed individually.

If you're not logged in, `MAP` gives the same `Not currently logged into
Blink service.` message as `VERIFY`/`GET`/`LOGOUT`.

## Two-factor logins

If Blink doesn't yet trust this device (see `HardwareId` above - this is common on
the very first login, and occasional afterwards), `LOGIN` will come back with:

```
Login to the Blink Service failed: Blink sent a two-factor verification code to
complete login; use VERIFY -v<code> to enter it.
```

Blink will have sent you a code by SMS or email. Enter it with:

```
VERIFY -v123456
```

- If it's correct, you'll get `Login to the Blink Service successful.` and can carry
  on as normal.
- If you mistype it, you'll get a failure message but can simply run `VERIFY` again
  with the right code - you do **not** need to run `LOGIN` again, as long as you
  don't wait so long that Blink's challenge itself expires.
- If you do run `LOGIN` again before finishing `VERIFY`, the old pending challenge is
  discarded and you'll need to use whichever code came with the newest `LOGIN`
  attempt.

## Troubleshooting

| Message | What it means |
|---|---|
| `Login to the Blink Service failed: Invalid credentials` | The email/password Blink received didn't match an account. Check `settings.json` or what you typed on the `LOGIN` line. |
| `...Blink sent a two-factor verification code...` | Expected on an untrusted device - see "Two-factor logins" above. |
| `...Blink did not confirm the verification code (...)` | The code was wrong, expired, or already used. Request a fresh one by running `LOGIN` again, or just retry `VERIFY` if it hasn't been too long. |
| `...Blink rejected the sign-in request (...)` / `...did not return an authorization code...` | Something changed on Blink's side of the login flow. This includes the raw response Blink sent, which is the most useful thing to include if you need to debug or report this. |
| `Not currently logged into Blink service.` | You ran `VERIFY`, `GET`, or `LOGOUT` before a successful `LOGIN`. |

If Blink has changed their login flow again and you're reading this because
something broke, start with [DEVELOPER.md's authentication section](DEVELOPER.md#6-authentication-deep-dive) -
it documents exactly which HTTP calls are made and why, so you can compare against
whatever Blink's app/site is now actually doing.
