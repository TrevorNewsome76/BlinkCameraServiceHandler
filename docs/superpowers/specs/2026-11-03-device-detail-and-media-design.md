# Device Detail, Snapshots & Video Retrieval — Design Spec

## Context

This is sub-project 2 of 4 in expanding BlinkCameraServiceHandler's command set
beyond login (see [2026-11-03-device-map-design.md](2026-11-03-device-map-design.md)
for sub-project 1, `MAP`, and the endpoint research it recorded). It folds together
what was originally planned as two separate later sub-projects — "triggers" and
"media download" — because in practice they share one architecture: look up a
device by name from already-fetched home-screen data, call Blink, optionally poll
an async command to completion, and (for media) write bytes to disk. "Alerts"
remains a separate, later sub-project.

The Map spec's endpoint research covered the shape of this area but was
reconstructed at a distance. Before writing this spec, the exact blinkpy source
(`api.py`, `camera.py`, `sync_module.py`, `helpers/util.py`) was re-read, and the
details below are more precise than — and in a couple of places correct —
that earlier summary. Most importantly:

- The Map spec's local-storage flow omitted that the clip "request" URL is also
  the download URL (the same path, POSTed to prepare, then GETed to fetch bytes)
  and the manifest's clip entries have no camera id or URL field, only
  `id`, `camera_name`, `created_at`, `size`.
- Thumbnail URLs are **not** a single flat field — the home-screen `thumbnail`
  value is either a bare timestamp (new API: build a fixed-shape URL from it) or
  a path that just needs `.jpg` appended (old API). Both cases are handled below.
- The `media/changed` (cloud clip list) response's exact field names were never
  confirmed against blinkpy's parsing code (it isn't parsed in the files that
  were read) — this spec's field names are a best-effort reconstruction, flagged
  as a risk in "Non-goals / known risks" below, to be fixed the same way the
  OAuth login flow was: from the exact error Blink returns when it's tried live.

## Problem

`MAP` can show that a camera exists and its cached status, but there is no way
from the terminal to: look at one device in more detail, ask Blink to capture a
fresh still image, pull down an image or video's actual bytes, or see what's been
recorded. Everything after "here's your device list" is still manual (log into
the Blink app on a phone).

## Goals

New commands, all operating on devices already known from a prior `MAP`/`GET`
call this session (no new auto-fetch — keeps the mental model "one logged-in
session, one fetched device list, several things you can do with it"):

- `DEVICE -n<name>` — detailed single-device view.
- `SNAP -n<name>` — trigger a fresh still image and wait for it to be ready.
- `IMAGE -n<name>` — download the device's current thumbnail to `media/`.
- `VIDEOS [-h<hours>]` — list recent **cloud-recorded** clips account-wide.
- `DOWNLOAD -i<id>` — download a cloud clip (by id from `VIDEOS`) to `media/`.
- `LOCALCLIPS -n<syncmodule>` — request and list that sync module's **SD-card**
  manifest.
- `LOCALDOWNLOAD -i<id> -n<syncmodule>` — prepare and download one SD-card clip
  to `media/` (requires `LOCALCLIPS` run for that sync module first, this
  session).

## Non-goals / known risks

- **Owls (Blink Mini), Sirens, Chimes, Doorbell_Buttons** stay out of scope for
  all seven commands, consistent with `MAP`'s existing "not detailed" treatment —
  they aren't modeled with names/ids at all yet, so "by name" lookup has nothing
  to look up. A dedicated follow-up spec would be needed to model them first.
- **Liveview (live video streaming)** is explicitly not implemented: Blink's
  liveview endpoint returns an `immis://` RTSP-ish stream URL, not a downloadable
  file, and a console app has no video player to point it at.
- **The `media/changed` response shape is reconstructed, not confirmed.** The
  field names used below (`media`, `thumbnail`, `device_name`, etc.) are the most
  likely shape based on the surrounding blinkpy code, not a response any of this
  research actually saw. Expect `VIDEOS`/`DOWNLOAD` to need one round of
  live-error-driven fixing after first real use, the same way the OAuth rewrite
  did — this is called out so it isn't mistaken for a regression later.
- **No delete-from-SD-card step.** blinkpy's local-storage flow has an optional
  "delete the clip off the card after downloading it" request. This spec
  deliberately leaves clips on the card/cloud buffer untouched — silently
  deleting footage off a user's physical SD card is a worse default than making
  them delete it from the Blink app if they want the space back.
- **No per-camera arm/disarm or motion-detection enable/disable** — genuinely a
  "trigger" in the original sense, but the user's clarified ask was images and
  video, not device configuration; left for a future spec if wanted.

## Design

### Shared components

- **`BlinkCommon/Interfaces/IApiMethods.cs`** — add:
  ```csharp
  byte[]? GetBytes(string url);
  ```
  `HttpClientApiHandler.GetBytes` calls `_httpClient.GetAsync(url).Result.Content.ReadAsByteArrayAsync().Result`
  (mirrors the existing synchronous `.Result` style of every other method on this
  class — no new async pattern introduced).

- **`BlinkCameraCall/Driver/CommandPolling.cs`** (new) — one static helper used
  by every trigger below:
  ```csharp
  public static bool WaitForCommandComplete(long networkId, long commandId, string tier)
  ```
  Polls `GET {tier-base}/network/{networkId}/command/{commandId}` once a second,
  up to 120 times (mirrors blinkpy's `MAX_RETRY=120`, `COMMAND_POLL_TIME=1`).
  Parses each response via `Duck.Implement<ICommandStatusResponse>`. Returns
  `true` on `Complete == true`, `false` if 120 attempts pass without completing.
  `tier-base` is always built the same way the rest of this codebase already
  does it: `$"https://rest-{tier}.immedia-semi.com"` (see `ApiTransactions`'s
  existing `AuthVerifyPin`/`SystemGetHomeScreen` — never `settings.BaseUrl`,
  which nothing in `ApiTransactions` actually reads).

  This helper is specifically for the `Complete`/`ICommandStatusResponse` shape
  used by `SNAP`, the `LOCALDOWNLOAD` prepare step, and the `LOCALCLIPS`
  manifest *request*. `LOCALCLIPS`'s second poll (waiting for the manifest's
  `clips` array to appear) has a different success condition and is its own
  small loop inline in that command, reusing the same 1s/120-try cadence but
  not this helper.

- **`BlinkCommon/Interfaces/System/ICommandStatusResponse.cs`** (new):
  ```csharp
  public interface ICommandStatusResponse
  {
      long Id { get; }
      long Network_Id { get; }
      bool Complete { get; }
      int Status_Code { get; }
  }
  ```

- **`BlinkCameraCall/Extensions/DeviceLookupExtension.cs`** (new) — the one
  piece of logic every command shares, so it's written and tested once:
  ```csharp
  public static IHomeScreenCamera? FindCameraByName(this ISessionDetails session, string name)
  public static IHomeScreenDoorbells? FindDoorbellByName(this ISessionDetails session, string name)
  public static IHomeScreenSyncModule? FindSyncModuleByName(this ISessionDetails session, string name)
  ```
  Case-insensitive exact match (`string.Equals(..., StringComparison.OrdinalIgnoreCase)`)
  against `session.Cameras`/`Doorbells`/`SyncModules`. Returns `null` on no match
  or more than one match — callers turn `null` into the same user-facing message
  (see Error handling), so ambiguity and "not found" are reported identically:
  "no single device/sync module named '<name>' was found; run MAP to see exact
  names." This is a deliberate simplification over trying to disambiguate
  same-named camera/doorbell pairs — YAGNI until an account actually hits it.

- **`BlinkCameraCall/Extensions/MediaFileExtension.cs`** (new):
  ```csharp
  public static string SaveMediaFile(string deviceName, byte[] bytes, string extension)
  ```
  Creates `media/` next to the executable if missing, sanitizes `deviceName` by
  replacing any character not in `[A-Za-z0-9 _-]` with `_`, and writes to
  `media/<SanitizedName>_<yyyyMMdd_HHmmss>.<extension>` (timestamp from
  `DateTime.Now`, so two downloads of the same device never collide). Returns
  the full path written, for the command's success message.

- **`BlinkCameraCall/Extensions/CommandProcessorExtension.cs`** — add
  `"device"`, `"snap"`, `"image"`, `"videos"`, `"download"`, `"localclips"`,
  `"localdownload"` to `ValidCommands`.

- **Argument extraction** (new, one file per command following the existing
  `HomeScreenExtensions`/`AuthenticationExtension` pattern — a single-letter
  prefix per argument, exactly as `LOGIN -u<x> -p<y>` already works):
  - `-n<name>` → device/sync-module name (used by `DEVICE`, `SNAP`, `IMAGE`,
    `LOCALCLIPS`, `LOCALDOWNLOAD`)
  - `-h<hours>` → optional lookback window for `VIDEOS`, default `24`
  - `-i<id>` → numeric id, for `DOWNLOAD` and `LOCALDOWNLOAD`

### `DEVICE -n<name>` — detailed single-device view

No new API call — formats data already sitting in `_sessionDetails` from the
last `MAP`/`GET`. Looks up the name as a camera first, then a doorbell. Example:

```
Camera: Front Door
  Network:        Home Front (armed)
  Status:         done
  Enabled:        true
  Battery:        ok
  Wifi signal:    5
  Firmware:       16.0.8
  Serial:         ABC123
  Local storage:  enabled, compatible
  Thumbnail:      1699000000 (run IMAGE to download it)
```

Doorbell output is the same shape, with a `Doorbell mode` line in place of
nothing extra, matching the fields `IHomeScreenDoorbells` actually has.

### `SNAP -n<name>` — trigger a fresh still image

1. Resolve the name to a camera or doorbell (error if neither/both match).
2. POST, no body:
   - camera: `{tier-base}/network/{network_id}/camera/{camera_id}/thumbnail`
   - doorbell: `{tier-base}/api/v1/accounts/{account_id}/networks/{network_id}/doorbells/{camera_id}/thumbnail`
3. Parse the POST response as `ICommandStatusResponse`; call
   `CommandPolling.WaitForCommandComplete`.
4. On success, re-fetch the home screen (`adapter.GetHomeScreen`) and refresh
   `_sessionDetails`'s cached lists — the device's `Thumbnail` field has changed,
   and `IMAGE` needs the new value.
5. Return `"New image captured for '<name>'. Run IMAGE -n<name> to download it."`
   or, on timeout, `"Blink did not finish capturing a new image for '<name>' in time."`

### `IMAGE -n<name>` — download the current thumbnail

1. Resolve the name (camera or doorbell).
2. Build the thumbnail URL from the device's cached `Thumbnail` field:
   - If it parses as an integer `ts`:
     `{tier-base}/api/v3/media/accounts/{account_id}/networks/{network_id}/{camera|doorbell}/{camera_id}/thumbnail/thumbnail.jpg?ts={ts}&ext=`
     (the literal segment is `camera` for a camera, `doorbell` for a doorbell).
   - Otherwise: the field value with `.jpg` appended, unless it already ends in
     `&ext=` — then used as-is.
3. `GetBytes` on that URL; on success, `SaveMediaFile(name, bytes, "jpg")`.
4. Return `"Saved image to <path>."` or, if the device has no thumbnail yet
   (empty field), `"'<name>' has no image yet — run SNAP -n<name> first."`

### `VIDEOS [-h<hours>]`

1. `GET {tier-base}/api/v1/accounts/{account_id}/media/changed?since={iso8601-utc}&page=1`
   where `since` is `DateTime.UtcNow.AddHours(-hours)` formatted
   `yyyy-MM-ddTHH:mm:ss+00:00`.
2. Parse into `List<ICloudMediaItem>` (new interface:
   `long Id, long Network_Id, long Device_Id, string Device_Name, DateTime Created_At, string Media, bool Deleted`),
   filtering out `Deleted == true` entries.
3. Cache the list on `BlinkLibrary` (`_lastCloudMedia`) for `DOWNLOAD` to use.
4. Print a numbered table: `Id | Device | Recorded at`. Empty list →
   `"No cloud video events in the last <hours> hours."`

### `DOWNLOAD -i<id>`

Looks up `id` in `_lastCloudMedia` (error: `"Run VIDEOS first, or '<id>' wasn't in the last VIDEOS list."` if not found or the cache is empty), `GetBytes` on `{tier-base}{item.Media}`, `SaveMediaFile(item.Device_Name, bytes, "mp4")`.

### `LOCALCLIPS -n<syncmodule>`

1. Resolve the sync module by name; error if its `Local_Storage_Status` isn't
   `"active"` (`"'<name>' doesn't have active local storage."`) — matches
   blinkpy's own enablement check.
2. `POST {tier-base}/api/v1/accounts/{account_id}/networks/{network_id}/sync_modules/{sync_id}/local_storage/manifest/request`,
   parse the `id` field from the response as the manifest *request* id.
3. Poll `GET {tier-base}/.../local_storage/manifest/request/{request_id}` (same
   1s/120-try shape as `CommandPolling`, but checking for a `clips` field
   present rather than `Complete`) until it returns `{manifest_id, clips: [...]}`.
4. Cache `(sync module id, manifest_id, clips)` on `BlinkLibrary`
   (`_lastLocalManifest`) — overwrites any previous one, consistent with
   `VIDEOS`/`DOWNLOAD`'s single-cache design.
5. Print a numbered table: `Id | Camera | Recorded at | Size`. Empty list →
   `"No clips on '<name>''s SD card."`

### `LOCALDOWNLOAD -i<id> -n<syncmodule>`

1. Resolve the sync module by name; confirm it matches the sync module id cached
   by the last `LOCALCLIPS` call (error otherwise: `"Run LOCALCLIPS -n<syncmodule> first."`).
2. Confirm `id` is one of the cached clips' ids (error: `"'<id>' wasn't in the last LOCALCLIPS list for '<name>'."`).
3. Build the fixed clip URL:
   `{tier-base}/api/v1/accounts/{account_id}/networks/{network_id}/sync_modules/{sync_id}/local_storage/manifest/{manifest_id}/clip/request/{clip_id}`
4. `POST` that URL (no body) to prepare the clip; parse the response as
   `ICommandStatusResponse`; `CommandPolling.WaitForCommandComplete`.
5. On success, `GetBytes` the **same URL** (this is the one genuinely unusual
   step in this whole spec — the request URL doubles as the download URL once
   the command completes); `SaveMediaFile(cameraName, bytes, "mp4")`, where
   `cameraName` comes from the cached clip entry's `camera_name`.
6. Timeout/failure messages mirror `SNAP`'s.

### Error handling (common to all seven commands)

| Condition | Output |
|---|---|
| Not logged in | `"Not currently logged into Blink service."` (matches every existing command) |
| No `MAP`/`GET` run yet this session (`_sessionDetails.Cameras` etc. all null) | `"Run MAP or GET first to load your devices."` |
| Name/id not found or ambiguous | See each command's section above — always names the exact command to run to fix it |
| Command polling times out | `"Blink did not finish <action> for '<name>' in time."` |
| Blink returns a non-success HTTP status at any step | Surface the raw status/body, matching the existing login-flow convention of showing Blink's actual response rather than a generic message |

### Testing

- `UnitTest_DeviceLookupExtension` (new) — exact match, case-insensitive match,
  no match, ambiguous match (two cameras/doorbells sharing a name), for all three
  `Find*ByName` helpers.
- `UnitTest_MediaFileExtension` (new) — filename sanitization (spaces kept,
  disallowed characters replaced), directory auto-creation, against a scratch
  temp directory (never the real `media/` folder).
- `UnitTest_CommandPolling` (new) — completes on first poll, completes after
  several polls, times out after 120 — using a fake `IApiMethods` returning
  canned JSON per call rather than real HTTP, so the test doesn't take two
  minutes to run the timeout case (inject a zero/near-zero poll interval for
  tests, matching how `MockAdapter` already stands in for the real adapter
  elsewhere in this test suite).
- `UnitTest_BlinkLibrary` additions, one pair (`Test_<Command>FailedNotLoggedIn`,
  `Test_<Command>Successful`) per new command, following the existing
  `Test_MapFailedNotLoggedIn`/`Test_MapSuccessful` pattern — plus the
  not-found/ambiguous/timeout cases called out above where they're new
  behavior `MAP` didn't have.

## Relationship to the remaining sub-project

**Alerts** (sub-project 3 of the original 4 — the Map spec's sub-project 4 was
renumbered when Triggers and Media merged into this spec) still has no written
spec. It will want this spec's `VIDEOS`/`media-changed` polling as its
"what happened recently" data source — Blink has no push/webhook mechanism, so
alerting can only mean periodically re-running that same cloud-media query (and
possibly `GET /events/network/{network}`) and diffing against what was already
seen. No shared code is extracted for that ahead of time (YAGNI), but the next
spec should build on this one's understanding of `media/changed` rather than
re-deriving it — and should re-verify this spec's reconstructed field names
first, since real usage here will have already found and fixed any that were
wrong.
