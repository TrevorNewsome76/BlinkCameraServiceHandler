# User Guide

BlinkCameraServiceHandler is a Windows console application for logging into a
personal [Blink](https://blinkforhome.com/) home-security account and running a
small set of commands against it: login (including two-factor codes), logout,
client-trust PIN verification, fetching your "home screen" (networks, sync
modules, cameras, doorbells), printing a readable device map, inspecting a
single device, triggering and downloading fresh snapshots, listing/
downloading cloud and SD-card video clips, navigating hubs and devices
as a `cd`/`ls` tree instead of repeating a device name on every command,
and watching for new doorbell/motion events in the background with
console alerts.

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

## First run: `config.json`

On first run the app creates (next to the executable) a `config.json` holding your
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

### Automatic login

If `config.json` already has a real `Email`/`Password` (not the shipped
placeholders), the app logs in automatically at startup instead of waiting
for you to type `LOGIN`:

```
Blink Camera Terminal
Automatically logging in...
Login to the Blink Service successful.
```

If Blink asks for a two-factor code, you're prompted for just the code
(no `VERIFY -v` needed) right there at startup:

```
Automatically logging in...
Enter the verification code Blink sent you: 123456
Login to the Blink Service successful.
```

You get up to 3 attempts at the code before it gives up:

```
Automatically logging in...
Enter the verification code Blink sent you: 000000
Login to the Blink Service failed: ...
Enter the verification code Blink sent you: 111111
Login to the Blink Service failed: ...
Enter the verification code Blink sent you: 222222
Login to the Blink Service failed: ...
Authentication failed. Check settings in config.
```

After that it drops you at the normal prompt, not logged in — run `LOGIN`
manually when you're ready. Whenever a login fully succeeds (automatic or
manual), the email/password that were actually used are saved back into
`config.json`, so a successful manual `LOGIN -u<email> -p<password>` the
first time sets you up for automatic login on every run after that.

Once you're logged in — automatically or manually, including right after a
`VERIFY` that completes a two-factor challenge — the app also starts
**watching for new doorbell/motion events in the background**, with no
extra command needed. See
["Watching for new events" below](#watching-for-new-events-watch--unwatch)
for what that looks like and how to stop/restart it.

## Commands

| Command | Syntax | What it does |
|---|---|---|
| Login (using config.json) | `LOGIN` | Logs in with the `Email`/`Password` already in `config.json`. |
| Login (explicit credentials) | `LOGIN -u<email> -p<password>` | Logs in with the given email/password instead of what's in `config.json`. No space between `-u`/`-p` and the value. |
| Verify a code | `VERIFY -v<code>` | Enters a verification code. Does one of two things depending on what just happened (see "Two-factor logins" below): completes a pending two-factor login, or verifies an old-style client-trust PIN. |
| Get home screen info | `GET -ihome` | Fetches your account's networks, sync modules, cameras and doorbells. Requires being logged in first. |
| Show a device map | `MAP` | Fetches the same data as `GET -ihome` and prints it as a readable tree: each network, with its sync module, cameras and doorbells nested underneath, each showing online/armed/enabled status, battery, wifi signal and firmware version. Requires being logged in first. |
| Single device detail | `DEVICE -n<name>` | Shows a detailed view of one camera or doorbell, using data already fetched by `MAP`/`GET`. |
| Trigger a snapshot | `SNAP -n<name>` | Asks the camera/doorbell to capture a fresh still image, and waits (up to ~2 minutes) until it's ready. |
| Download a thumbnail | `IMAGE -n<name>` | Downloads the device's current thumbnail to `media/` as a `.jpg`. |
| List cloud video events | `VIDEOS [-h<hours>]` | Lists recent cloud-recorded clips account-wide (default: last 24 hours). |
| Download a cloud clip | `DOWNLOAD -i<id>` | Downloads a cloud clip (by id from the last `VIDEOS` list) to `media/` as a `.mp4`. |
| List an SD card's clips | `LOCALCLIPS -n<syncmodule>` | Requests and lists the SD-card manifest for a sync module (takes a few seconds). |
| Download an SD-card clip | `LOCALDOWNLOAD -i<id> -n<syncmodule>` | Downloads one SD-card clip (by id from the last `LOCALCLIPS` list for that sync module) to `media/`. |
| Change directory | `CD <name>`, `CD ..`, `CD \` | Moves into a hub or device, back up a level, or straight back to the root of the tree. |
| List current directory | `LS` | Lists hubs at the root, devices in a hub, or a device's media, depending on where you are. |
| Trigger (current device) | `TRIGGER` | Takes a fresh snapshot of the device you're currently `cd`'d into. |
| Download by name | `DOWNLOAD <name>` | Downloads a media item by the name `LS` showed for it (device directory only). |
| Watch for new events | `WATCH [-s<seconds>]` | Starts watching for new doorbell/motion events in the background (default interval: 20 seconds). Starts automatically on login — see below. |
| Stop watching | `UNWATCH` | Stops the background watch started by `WATCH` (or automatically on login). |
| Logout | `LOGOUT` | Logs out of the current session (and stops any active `WATCH`). |
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

DEVICE -nFront Door
Camera: Front Door
  Network:        Home (armed)
  Status:         done
  Enabled:        true
  Battery:        ok
  Wifi signal:    5
  Firmware:       16.0.8
  Serial:         ABC123
  Local storage:  disabled, compatible
  Thumbnail:      none yet (run SNAP to capture one)

SNAP -nFront Door
New image captured for 'Front Door'. Run IMAGE -nFront Door to download it.

IMAGE -nFront Door
Saved image to media\Front_Door_20261103_143000.jpg.

VIDEOS -h48
[1] Id 2001  Device: Front Door  Recorded: 2026-11-02 09:00:00 UTC

DOWNLOAD -i2001
Saved video to media\Front_Door_20261103_143512.mp4.

BC> cd Home
Now in hub 'Home'.

BC\Home> cd Front Door
Now in device 'Front Door'.

BC\Home\Front Door> ls
current  [current]
2026-11-02_09-00-00  [cloud]

BC\Home\Front Door> trigger
New image captured for 'Front Door'. Run IMAGE -nFront Door to download it.

BC\Home\Front Door> download current
Saved image to media\Front_Door_20261103_143600.jpg.

BC\Home\Front Door> cd ..
Now in hub 'Home'.

BC\Home> cd \
Back to root.

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

## Navigating with CD/LS

Instead of repeating a device's name on every command, you can `cd` into it
once and run `TRIGGER`/`DOWNLOAD <name>` against "wherever you are." The
prompt itself tracks this: it starts as `BC>` at the root, grows to
`BC\<hub>>` once you `cd` into a hub, and grows again to
`BC\<hub>\<device>>` once you `cd` into a camera or doorbell.

- `CD <name>` moves down one level: from the root into a hub, or from a hub
  into one of its cameras/doorbells. The name must match exactly what `LS`
  shows (case-insensitive).
- `CD ..` moves up one level (device → hub → root). At the root, `CD ..` is
  a no-op.
- `CD \` (or `CD /`) jumps straight back to the root from anywhere.
- `LS` lists whatever is "in" your current directory: hubs at the root,
  that hub's cameras/doorbells one level down, or — inside a device — a
  merged listing of its current thumbnail plus its recent cloud and
  SD-card clips, each tagged `[current]`, `[cloud]`, or `[local]`.

`TRIGGER` and bare `DOWNLOAD <name>` only work inside a device directory
(`cd` into a camera or doorbell first), and `DOWNLOAD <name>` additionally
needs you to have run `LS` at least once since `cd`-ing there, since it
downloads whichever entry `LS` just showed under that name.

All of the original flag-style commands — including the legacy
`DOWNLOAD -i<id>` — keep working completely unchanged no matter where you
currently are in the tree; `cd`/`ls` are a convenience layered on top, not a
replacement.

| Message | What it means |
|---|---|
| `Not currently logged into Blink service.` | Log in first. |
| `Run MAP or GET first to load your devices.` | You're logged in, but haven't fetched your device list yet this session. |
| `No hub named '<name>' was found. Run LS to see exact names.` | `CD` at the root didn't match any hub. |
| `No device named '<name>' was found. Run LS to see exact names.` | `CD` inside a hub didn't match any of its cameras/doorbells. |
| `Not in a device directory. cd into a camera or doorbell first.` | `TRIGGER` or bare `DOWNLOAD` was run at the root or in a hub. |
| `Run LS first.` | Bare `DOWNLOAD <name>` was run before `LS` in this device directory (or after `cd`-ing elsewhere and back). |
| `'<name>' wasn't in the last LS output.` | The name didn't match any entry from the last `LS`. |

## Single device detail (`DEVICE`)

`DEVICE -n<name>` prints a detailed view of one camera or doorbell: network,
status, enabled/battery/wifi, firmware, serial, local-storage support, and
(for doorbells) doorbell mode. The name must match exactly what `MAP` shows
(case-insensitive). It uses data already fetched by `MAP`/`GET` this
session — it never calls Blink itself, so run `MAP` or `GET -ihome` first.

| Message | What it means |
|---|---|
| `Not currently logged into Blink service.` | Log in first. |
| `Run MAP or GET first to load your devices.` | You're logged in, but haven't fetched your device list yet this session. |
| `No single device named '<name>' was found. Run MAP to see exact names.` | The name didn't match exactly one camera/doorbell. |

## Triggering a fresh snapshot (`SNAP`)

`SNAP -n<name>` asks the named camera or doorbell to capture a brand-new
still image, waiting for Blink to confirm it's ready (up to about two
minutes — this is a real device operation, not instant). On success it
refreshes your cached device list so the new thumbnail is picked up by a
following `IMAGE` command.

| Message | What it means |
|---|---|
| `New image captured for '<name>'. Run IMAGE -n<name> to download it.` | Success — the device took a fresh picture. |
| `Blink did not finish capturing a new image for '<name>' in time.` | Blink never confirmed completion within the timeout. Try again. |
| `Error: Blink rejected the request: <raw response>` | Blink refused to even start the capture (busy, offline, expired token, etc). The raw response is included for debugging. |

Same `Not currently logged into Blink service.` / `Run MAP or GET first...` /
"not found" messages as `DEVICE` apply here too.

## Downloading a thumbnail (`IMAGE`)

`IMAGE -n<name>` downloads the device's *current* thumbnail (not a new one —
run `SNAP` first if you want the latest) to a `media/` folder created next
to the executable, named `<DeviceName>_<timestamp>.jpg`.

| Message | What it means |
|---|---|
| `Saved image to <path>.` | Success. |
| `'<name>' has no image yet — run SNAP -n<name> first.` | The device has never had a thumbnail captured. |
| `Blink did not return an image for '<name>'.` | The download itself failed (bad/expired URL, Blink-side error). Try SNAP then IMAGE again. |

## Cloud video clips (`VIDEOS` / `DOWNLOAD`)

`VIDEOS [-h<hours>]` lists recent cloud-recorded clips across your whole
account (default lookback: 24 hours), numbered so you can pick one.
`DOWNLOAD -i<id>` downloads the clip with that id — from the *most recent*
`VIDEOS` call — to `media/` as a `.mp4`.

| Message | What it means |
|---|---|
| `No cloud video events in the last <hours> hours.` | Nothing recorded in that window. |
| `Run VIDEOS first, or '<id>' wasn't in the last VIDEOS list.` | `DOWNLOAD` was run without a matching `VIDEOS` call first, or with a stale/wrong id. |
| `Blink did not return a clip for '<id>'.` | The download itself failed (bad/expired URL, Blink-side error). Run `VIDEOS` again and retry. |

Note: the exact shape of Blink's cloud-media response was reconstructed
from a third-party open-source client rather than confirmed live, so these
two commands are the most likely in this release to need a fix after
real-world use — see [DEVELOPER.md](DEVELOPER.md) if that happens.

## Watching for new events (`WATCH` / `UNWATCH`)

`WATCH [-s<seconds>]` polls your account in the background for new
doorbell/motion cloud events (the same data `VIDEOS` shows you on demand)
and alerts you the moment a new one shows up — without taking over the
shell. You can keep typing other commands while it runs:

```
BC> watch
Watching for new events every 20s. Use UNWATCH to stop.
BC> map
...
BC> [ALERT] New event: Front Door at 2026-11-09 14:32:05 UTC (id 3001)
BC>
```

- It starts **automatically** the moment you're logged in (see "Automatic
  login" above) — you only need to type `WATCH` yourself if you've since
  run `UNWATCH` and want to restart it, or want a different interval.
- `-s<seconds>` sets how often it checks (default 20). `UNWATCH` stops it;
  running `WATCH` again afterwards (with or without `-s`) restarts it.
- Each alert also triggers a console beep, so you'll notice it even if
  you're not looking at the screen.
- `LOGOUT` stops any active watch too — it won't keep polling with an
  invalidated session after you log out.

| Message | What it means |
|---|---|
| `Not currently logged into Blink service.` | Log in first. |
| `Watching for new events every <seconds>s. Use UNWATCH to stop.` | Success. |
| `Already watching. Use UNWATCH to stop.` | `WATCH` was run while one was already active. |
| `Not currently watching.` | `UNWATCH` was run with nothing active. |
| `Stopped watching.` | Success. |
| `[WATCH] Error checking for new events: <message>` | A single poll failed (network hiccup, etc) — watching keeps running and retries on the next interval. |

Note: like `VIDEOS`/`DOWNLOAD` (above), this polls Blink's cloud-media
endpoint rather than receiving a live push, so there's up to one poll
interval of delay before a real-world event shows up as an alert here.

## SD-card video clips (`LOCALCLIPS` / `LOCALDOWNLOAD`)

If a Sync Module has an SD card with local storage enabled, `LOCALCLIPS
-n<syncmodule>` asks Blink to read the card's manifest (this takes a few
seconds — Blink has to go read the card) and lists the clips on it.
`LOCALDOWNLOAD -i<id> -n<syncmodule>` then downloads one of those clips to
`media/` — you must run `LOCALCLIPS` for that same sync module first in
this session; its clip ids aren't valid across different sync modules or
after a session restart.

| Message | What it means |
|---|---|
| `'<name>' doesn't have active local storage.` | That sync module has no SD card, or it isn't active. |
| `No clips on '<name>''s SD card.` | The card is empty. |
| `Run LOCALCLIPS -n<syncmodule> first.` | `LOCALDOWNLOAD` was run before `LOCALCLIPS` for that sync module, or the cached manifest is for a different one. |
| `'<id>' wasn't in the last LOCALCLIPS list for '<name>'.` | The clip id wasn't in that sync module's last listed manifest. |
| `Blink did not finish reading the SD-card manifest for '<name>' in time.` | `LOCALCLIPS` never got a ready manifest within the timeout. Try again. |
| `Blink did not finish preparing the clip for '<name>' in time.` | Blink never finished pulling the clip off the card within the timeout. Try again. |
| `Blink did not return the clip for '<name>'.` | The download itself failed after Blink finished preparing it. Try again. |
| `Error: Blink rejected the request: <raw response>` | Blink refused to even start the manifest read or clip prepare (busy, offline, expired token, etc). The raw response is included for debugging. |

Clips are **not** deleted from the SD card after downloading — use the
Blink app if you want to free up card space.

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
| `Login to the Blink Service failed: Invalid credentials` | The email/password Blink received didn't match an account. Check `config.json` or what you typed on the `LOGIN` line. |
| `...Blink sent a two-factor verification code...` | Expected on an untrusted device - see "Two-factor logins" above. |
| `...Blink did not confirm the verification code (...)` | The code was wrong, expired, or already used. Request a fresh one by running `LOGIN` again, or just retry `VERIFY` if it hasn't been too long. |
| `...Blink rejected the sign-in request (...)` / `...did not return an authorization code...` | Something changed on Blink's side of the login flow. This includes the raw response Blink sent, which is the most useful thing to include if you need to debug or report this. |
| `Not currently logged into Blink service.` | You ran `VERIFY`, `GET`, or `LOGOUT` before a successful `LOGIN`. |

If Blink has changed their login flow again and you're reading this because
something broke, start with [DEVELOPER.md's authentication section](DEVELOPER.md#6-authentication-deep-dive) -
it documents exactly which HTTP calls are made and why, so you can compare against
whatever Blink's app/site is now actually doing.
