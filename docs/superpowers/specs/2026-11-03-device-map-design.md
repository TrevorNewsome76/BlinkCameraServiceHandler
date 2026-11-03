# Device/Network Map — Design Spec

## Context

This is sub-project 1 of 4 in expanding BlinkCameraServiceHandler's command set
beyond login. The other three (device triggers, media download, background event
alerts) are separate, later specs — see "Relationship to later sub-projects" below.
Full endpoint research backing this whole initiative (triggers, media, events) is
recorded here so it isn't lost before those specs are written:

### Endpoints available (from blinkpy, the reference OSS client), for later use

- **Triggers** (all POST, each polls `GET {base}/network/{network}/command/{id}`
  until `complete: true` — see "Command polling" below):
  - Arm/disarm whole network: `.../api/v1/accounts/{account}/networks/{network}/state/arm|disarm`
  - Enable/disable camera motion detection — path depends on camera type:
    - default camera: `/network/{network}/camera/{camera_id}/enable|disable`
    - doorbell: `/api/v1/accounts/{account}/networks/{network}/doorbells/{camera_id}/enable|disable`
    - mini/owl: `POST .../owls/{camera_id}/config` body `{"enabled": true|false}`
  - Trigger new snapshot: `.../camera|doorbells|owls/{id}/thumbnail`
  - Trigger new clip: `.../camera|doorbells|owls/{id}/clip`
  - Start liveview (returns an `immis://` stream URL, not directly playable by a
    console app): `/api/v6/.../cameras/{id}/liveview` (default), `/api/v2/.../owls/{id}/liveview`
    (mini), `/api/v2/.../doorbells/{id}/liveview` (doorbell)
  - Floodlight on/off (Mini only): `.../owls/{camera_id}/lights/{on|off}`
  - **No siren or lock/unlock endpoint exists in this API.**
- **Media**:
  - Cloud clips/motion feed: `GET /api/v1/accounts/{account}/media/changed?since={ts}&page={page}`
    → each entry has a `media` path (clip) and `created_at`; download is a plain
    `GET` on `{base}{media_path}`.
  - Thumbnails: no listing endpoint; a thumbnail URL comes from the camera's
    `config["thumbnail"]` field (home-screen response) or is refreshed via the
    snapshot trigger above.
  - SD-card ("local storage") clips: 3-step — `POST .../sync_modules/{sync_id}/local_storage/manifest/request`
    → poll `GET .../manifest/request/{request_id}` until it has `clips` → for each
    clip, `POST` a clip-specific URL (built from a template keyed by
    account/network/sync/manifest/clip id) to ask the Sync Module to upload it to
    the cloud, poll the command, then `GET` that same URL to download the bytes,
    and optionally `POST` the same URL with `request` replaced by `delete` to
    remove it from the card afterward.
- **Events**: `GET /events/network/{network}` (sync-module-level event log) and the
  `media/changed` feed above double as the only sources of "what happened" — both
  are pull-only; Blink has no push/webhook mechanism in this API.
- **Command polling** (used by every trigger above): after any action POST, poll
  `GET /network/{network}/command/{command_id}` once per second, up to 120 times;
  treat `status_code != 908` as immediate failure, `complete: true` as success,
  and exhausting retries as a timeout failure.

This spec itself only covers the Map feature below; the trigger/media/event
mechanics above are recorded for when those specs are written, not implemented now.

## Problem

`GET -ihome` already fetches the full home-screen response (networks, sync
modules, cameras, doorbells) but only ever prints `"Command ran successfully."` —
none of that data is ever actually shown to the user. There is no command that
displays a readable picture of what devices exist, which network/sync module they
belong to, or their current status.

## Goals

- A new `MAP` command that fetches the home screen and prints a readable
  Network → Sync Module → Cameras/Doorbells tree with each device's key status
  fields (online/armed/enabled, battery, wifi signal, firmware version).
- Leave the existing `GET -ihome` command's behavior unchanged.
- Keep the formatting logic in a pure, independently-unit-testable function.

## Non-goals

- Properly modeling `Sirens`/`Chimes`/`Owls`/`Doorbell_Buttons` (currently raw
  `string[]` placeholders on `IGetHomeScreenResponse`) — shown as a bare count
  instead. Worth a dedicated follow-up if those accessory types matter.
- Triggering any action, downloading any media, or polling for events — those are
  separate sub-projects (see above).
- Fixing `IHomeScreenSyncModule.Network_Id` being typed `string` while every other
  `Network_Id` in the codebase is `long` — worked around in the grouping code
  instead of changed, since other things may already depend on its current type.

## Design

### Components

- **`BlinkCameraCall/Extensions/DeviceMapExtension.cs`** (new) — pure formatting:
  ```csharp
  public static string ToDeviceMapString(this IGetHomeScreenResponse response)
  ```
  Groups `Sync_Modules`, `Cameras`, and `Doorbells` under the `Networks` entry
  whose `Id` they reference, in network list order; devices with no matching
  network (or an empty response) are reported, not silently dropped. No I/O, no
  Shelf/adapter access — just data in, formatted string out, so it's testable
  directly against a hand-built `IGetHomeScreenResponse` with no mocking.

- **`BlinkLibrary.Map()`** (new method) — mirrors the existing `Get()` method's
  guard clause and fetch call, then formats:
  ```csharp
  public string Map()
  {
      if (!(_sessionDetails?.LoggedInStatus ?? false))
          return "Not currently logged into Blink service.";

      var response = adapter.GetHomeScreen(_sessionDetails.Account!);
      _sessionDetails.Networks = response?.Networks;
      _sessionDetails.SyncModules = response?.Sync_Modules;
      _sessionDetails.Cameras = response?.Cameras;
      _sessionDetails.Doorbells = response?.Doorbells;

      return response is null
          ? "No devices found."
          : response.ToDeviceMapString();
  }
  ```
  (Session-state fields are updated the same way `Get()` already does it, so `MAP`
  and `GET -ihome` stay consistent with each other.)

- **`Program.cs`** — add a `case "map": Console.WriteLine(blinkLibrary.Map()); break;`
  arm to the switch, plus a `HELP` line.

- **`BlinkCameraCall/Extensions/CommandProcessorExtension.cs`** — add `"map"` to
  `ValidCommands`.

### Output format

```
Network: Home Front (armed)
  Sync Module: My Blink Sync Module  [online, fw 16.0.8, wifi 5]
  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]
  Camera: Garage  [enabled, status done, battery ok, wifi 5]
  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]

Network: Home Back (disarmed)
  Sync Module: My Blink Sync Module  [offline, fw 4.4.8, wifi 5]
  (no cameras or doorbells)

Owls: 0 (not detailed)
Sirens: 0 (not detailed)
Chimes: 0 (not detailed)
Doorbell_Buttons: 0 (not detailed)
```

The accessory-count lines only appear when at least one of those arrays is
non-empty, so a typical account without those device types doesn't see clutter.
Likewise, a trailing `"Unassigned devices:"` section (see Error handling below)
only appears if a sync module/camera/doorbell's `Network_Id` doesn't match any
network in the response — the example above omits it because none of
`MockData.CreateValidHomeScreenResponse()`'s devices are unassigned.

### Error handling

| Condition | Output |
|---|---|
| Not logged in | `"Not currently logged into Blink service."` (matches `VERIFY`/`GET`'s existing wording) |
| `GetHomeScreen` returns null | `"No devices found."` |
| Response has networks but zero cameras/doorbells/sync modules under one | `"(no cameras or doorbells)"` under that network, not an empty line |
| A sync module/camera/doorbell's `Network_Id` doesn't match any network in the list | Listed in a trailing `"Unassigned devices:"` section rather than silently dropped |

### Testing

- `UnitTest_DeviceMapExtension` (new) — asserts the exact formatted string against
  `MockData.CreateValidHomeScreenResponse()` (already has 2 networks, 2 sync
  modules, 2 cameras, 1 doorbell — covers the grouping logic), plus a case for a
  network with no devices and a case for an unassigned device.
- `UnitTest_BlinkLibrary` additions: `Test_MapFailedNotLoggedIn` (mirrors
  `Test_VerifyPinFailedNotLoggedIn`) and `Test_MapSuccessful` (mirrors the existing
  `GET`-path test pattern, using `MockAdapter`/`MockData.CreateValidHomeScreenResponse()`).

## Relationship to later sub-projects

This spec's `ToDeviceMapString` groups devices by network/sync-module — the same
grouping the **Triggers** sub-project will need to resolve "arm the network this
camera belongs to" or "which sync module owns this camera's SD card." No shared
code is extracted preemptively (YAGNI), but the next spec should re-use this
spec's understanding of the data shape rather than re-deriving it.
