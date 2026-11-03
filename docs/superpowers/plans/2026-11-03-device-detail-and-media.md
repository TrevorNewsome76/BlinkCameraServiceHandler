# Device Detail, Snapshots & Video Retrieval Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add seven new terminal commands — `DEVICE`, `SNAP`, `IMAGE`, `VIDEOS`,
`DOWNLOAD`, `LOCALCLIPS`, `LOCALDOWNLOAD` — covering per-device detail, on-demand
still images, and cloud + SD-card video retrieval.

**Architecture:** Every command operates on the home-screen data already fetched
by `MAP`/`GET` this session (no new auto-fetch). New API calls go through
`ApiTransactions`/`IApiTransactions` exactly like existing ones, routed through
`AdapterExtension`/`IAdapter` into `BlinkLibrary`. Shared plumbing (binary
download, async command polling, device-by-name lookup, media file saving) is
built once in Task 1 and reused by every later task.

**Tech Stack:** C# / .NET 8, Shadow.Quack duck typing, xUnit + FluentAssertions,
the existing synchronous (`.Result`-based, no `async`/`await`) HTTP style.

**Spec:** [2026-11-03-device-detail-and-media-design.md](../specs/2026-11-03-device-detail-and-media-design.md)

## Global Constraints

- No `async`/`await` anywhere — every HTTP call in this codebase is synchronous
  (`.Result`), and new code follows the same style.
- URLs are always built from `account.Tier` as `$"https://rest-{tier}.immedia-semi.com"`,
  never from `settings.BaseUrl` (nothing in `ApiTransactions` reads that field).
- Owls (Blink Mini), Sirens, Chimes, and Doorbell_Buttons are out of scope for
  every command in this plan — they have no name/id to look up by.
- No liveview streaming, no per-camera arm/disarm or motion-detection toggling,
  and no delete-from-SD-card step after a local-storage download — all
  explicitly deferred by the spec.
- Device/sync-module name lookups are case-insensitive exact matches; no match
  or more than one match both produce the same class of "not found" message
  (see spec's Error handling table) — no fuzzy matching, no disambiguation UI.
- The `media/changed` response shape (`ICloudMediaItem`'s fields) is a
  best-effort reconstruction, not a confirmed live response. Treat its first
  live run as expected to need a fix, the same way the OAuth rewrite did.

## Review Focus

- A device/sync-module command is run while logged in but before `MAP`/`GET`
  has ever populated `_sessionDetails` this session (lists are `null`, not
  empty) — every new `BlinkLibrary` method must guard this distinctly from
  "not logged in" rather than null-reference-crashing on an empty-list LINQ
  call.
- Two cameras/doorbells (or two sync modules) sharing a name — lookup must
  report the same "not found" class of message as a true miss, not throw or
  silently pick one.
- `VIDEOS` called with a missing or non-numeric `-h` value — must default to
  24 hours rather than throwing.
- `LOCALDOWNLOAD` for a clip id that belonged to a *different* sync module's
  last `LOCALCLIPS` call (stale cache after switching sync modules) — must be
  rejected with a clear message, not silently downloaded against the wrong
  manifest.
- Every async command (`SNAP`'s capture, `LOCALCLIPS`'s manifest,
  `LOCALDOWNLOAD`'s prepare step) hitting its retry ceiling — must produce a
  distinct timeout message, not be mistaken for success or for a generic
  Blink-rejected error.

---

## Task 1: Shared plumbing

**Files:**
- Modify: `BlinkCommon/Interfaces/IApiMethods.cs`
- Modify: `BlinkCameraCall/Driver/HttpClientApiHandler.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Create: `BlinkCommon/Interfaces/System/ICommandStatusResponse.cs`
- Create: `BlinkCameraCall/Driver/CommandPolling.cs`
- Create: `BlinkCameraCall/Extensions/DeviceLookupExtension.cs`
- Create: `BlinkCameraCall/Extensions/MediaFileExtension.cs`
- Test: `BlinkCameraCallUnitTests/Driver/UnitTest_CommandPolling.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceLookupExtension.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_MediaFileExtension.cs`

**Interfaces:**
- Produces: `IApiMethods.GetBytes(string url) : byte[]?`; `IApiTransactions.DownloadBytes(string url) : byte[]?`;
  `IAdapter` extension `DownloadBytes(this IAdapter adapter, string url) : byte[]?`;
  `ICommandStatusResponse { long Id; long Network_Id; bool Complete; int Status_Code; }`;
  `CommandPolling.WaitForCommandComplete(IApiMethods api, string commandStatusUrl, TimeSpan? pollInterval = null, int maxRetries = 120) : ICommandStatusResponse?`
  (returns the last-polled status once `Complete == true`, or `null` after
  `maxRetries` attempts);
  `DeviceLookupExtension.FindCameraByName(this ISessionDetails session, string name) : IHomeScreenCamera?`,
  `.FindDoorbellByName(...) : IHomeScreenDoorbells?`, `.FindSyncModuleByName(...) : IHomeScreenSyncModule?`
  (each returns `null` on zero or 2+ case-insensitive matches, or if the
  underlying list is `null`);
  `MediaFileExtension.SaveMediaFile(string deviceName, byte[] bytes, string extension, string? mediaDirectory = null) : string`
  (returns the full path written; creates `mediaDirectory ?? Path.Combine(AppContext.BaseDirectory, "media")`
  if missing; sanitizes `deviceName` by replacing any character outside
  `[A-Za-z0-9 _-]` with `_`; names the file `<SanitizedName>_<yyyyMMdd_HHmmss>.<extension>`).
- Consumes: nothing new — only existing `ISessionDetails`, `IHomeScreenCamera`,
  `IHomeScreenDoorbells`, `IHomeScreenSyncModule`.

- [ ] **Step 1: Write failing tests for `DeviceLookupExtension`**

```csharp
// UnitTest_DeviceLookupExtension.cs
[Fact] public void Test_FindCameraByName_ExactMatch_ReturnsCamera()
[Fact] public void Test_FindCameraByName_CaseInsensitiveMatch_ReturnsCamera()
[Fact] public void Test_FindCameraByName_NoMatch_ReturnsNull()
[Fact] public void Test_FindCameraByName_AmbiguousMatch_ReturnsNull()
[Fact] public void Test_FindCameraByName_NullList_ReturnsNull()
```
Build each `ISessionDetails` with `Duck.Implement<ISessionDetails>(new { Cameras = ... })`
(reuse `MockData.CreateValidHomeScreenCameras()` style inline lists). Mirror the
same five cases for `FindDoorbellByName` and `FindSyncModuleByName` (15 facts
total, or parameterized — implementer's choice).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter UnitTest_DeviceLookupExtension`
Expected: FAIL — `DeviceLookupExtension` does not exist.

- [ ] **Step 3: Implement `DeviceLookupExtension`**

One file, three near-identical extension methods per the signatures above.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter UnitTest_DeviceLookupExtension`
Expected: PASS, all facts.

- [ ] **Step 5: Write failing tests for `MediaFileExtension`**

```csharp
[Fact] public void Test_SaveMediaFile_SanitizesDisallowedCharacters()
[Fact] public void Test_SaveMediaFile_CreatesDirectoryIfMissing()
[Fact] public void Test_SaveMediaFile_WritesExactBytes()
```
Pass an explicit `mediaDirectory` under `Path.GetTempPath()` (a fresh
`Guid.NewGuid()`-named subfolder per test, deleted in a `finally`/`Dispose`) —
never the real `media/` folder next to the test runner.

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test --filter UnitTest_MediaFileExtension`
Expected: FAIL — `MediaFileExtension` does not exist.

- [ ] **Step 7: Implement `MediaFileExtension`**

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test --filter UnitTest_MediaFileExtension`
Expected: PASS.

- [ ] **Step 9: Write failing tests for `CommandPolling`**

```csharp
[Fact] public void Test_WaitForCommandComplete_CompletesOnFirstPoll()
[Fact] public void Test_WaitForCommandComplete_CompletesAfterSeveralPolls()
[Fact] public void Test_WaitForCommandComplete_TimesOutAfterMaxRetries_ReturnsNull()
```
Use a hand-rolled `IApiMethods` test double (not `MockHttpClientApiHandler` —
this one needs a call counter) whose `Get` returns
`"{\"id\":1,\"network_id\":2,\"complete\":false,\"status_code\":908}"` for the
first N calls and `"...\"complete\":true...\"` after; pass `pollInterval: TimeSpan.Zero`
and a small `maxRetries` (e.g. 3) for the timeout case so the test runs instantly.

- [ ] **Step 10: Run tests to verify they fail**

Run: `dotnet test --filter UnitTest_CommandPolling`
Expected: FAIL — `CommandPolling` does not exist.

- [ ] **Step 11: Implement `ICommandStatusResponse` and `CommandPolling`**

`CommandPolling.WaitForCommandComplete` loops: `api.Get(commandStatusUrl).Deserialize<ICommandStatusResponse>()`;
return it immediately if `Complete`; otherwise `Thread.Sleep(pollInterval ?? TimeSpan.FromSeconds(1))`
and retry, up to `maxRetries` times; return `null` if exhausted.

- [ ] **Step 12: Run tests to verify they pass**

Run: `dotnet test --filter UnitTest_CommandPolling`
Expected: PASS.

- [ ] **Step 13: Add `GetBytes` to `IApiMethods` and its implementations**

`HttpClientApiHandler.GetBytes`: `_httpClient.GetAsync(url).Result.Content.ReadAsByteArrayAsync().Result`.
`MockHttpClientApiHandler.GetBytes`: add a `switch (Url)` mirroring the existing
`Get` method's style, `throw new NotImplementedException()` for unmatched URLs
(later tasks add cases as they need them — no case needed yet in this task).
No new test here: this mirrors the existing untested `Get`/`Post` wrappers.

- [ ] **Step 14: Add `DownloadBytes` to `IApiTransactions`/`ApiTransactions`/`AdapterExtension`**

```csharp
byte[]? DownloadBytes(string url); // IApiTransactions
public byte[]? DownloadBytes(string url) => ApiDriver?.GetBytes(url);  // ApiTransactions
public static byte[]? DownloadBytes(this IAdapter adapter, string url) => Transactions.DownloadBytes(url) ?? null; // AdapterExtension
```

- [ ] **Step 15: Run the full test suite**

Run: `dotnet test`
Expected: PASS, no regressions (the project must still compile — `GetBytes` was
added to an interface with two implementers, both now updated).

- [ ] **Step 16: Commit**

```bash
git add BlinkCommon/Interfaces/IApiMethods.cs BlinkCameraCall/Driver/HttpClientApiHandler.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCommon/Interfaces/IApiTransactions.cs \
  BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs \
  BlinkCommon/Interfaces/System/ICommandStatusResponse.cs BlinkCameraCall/Driver/CommandPolling.cs \
  BlinkCameraCall/Extensions/DeviceLookupExtension.cs BlinkCameraCall/Extensions/MediaFileExtension.cs \
  BlinkCameraCallUnitTests/Driver/UnitTest_CommandPolling.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceLookupExtension.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_MediaFileExtension.cs
git commit -m "Add shared plumbing for device lookup, binary download, and command polling"
```

---

## Task 2: `SNAP` — trigger a fresh still image

**Files:**
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Create: `BlinkCameraCall/Extensions/MediaCommandExtensions.cs` (device-name
  argument extraction, shared by every task below)
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_MediaCommandExtensions.cs`

**Interfaces:**
- Consumes (Task 1): `CommandPolling.WaitForCommandComplete`, `ICommandStatusResponse`,
  `DeviceLookupExtension.FindCameraByName`/`FindDoorbellByName`.
- Produces: `MediaCommandExtensions.ExtractDeviceName(this string[]? arguments) : string`
  (reads the `-n` argument the same way `ExtractPinCode` reads `-v`; throws
  `ArgumentException("Device name not supplied. Use -n<name>.")` if missing —
  later tasks reuse this unchanged);
  `IApiTransactions.SystemTriggerCameraSnapshot(IAuthAccount account, long networkId, long cameraId) : ICommandStatusResponse?`,
  `.SystemTriggerDoorbellSnapshot(IAuthAccount account, long networkId, long doorbellId) : ICommandStatusResponse?`
  (each: `POST` with no body to the path from the spec's `SNAP` section, then
  `CommandPolling.WaitForCommandComplete` on `{tier-base}/network/{networkId}/command/{response.Id}`,
  returning the final status or `null` on timeout);
  `AdapterExtension.TriggerCameraSnapshot`/`TriggerDoorbellSnapshot` (thin
  pass-throughs, same shape as the existing `VerifyPin`/`Logout` wrappers);
  `BlinkLibrary.Snap(string[] arguments) : string`.

- [ ] **Step 1: Write failing tests for `ExtractDeviceName`**

```csharp
[Fact] public void Test_ExtractDeviceName_Successful()
[Fact] public void Test_ExtractDeviceName_MultiWordName_Successful()
[Fact] public void Test_ExtractDeviceName_MissingArgument_Throws()
[Fact] public void Test_ExtractDeviceName_NullArguments_Throws()
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter UnitTest_MediaCommandExtensions`
Expected: FAIL — method does not exist.

- [ ] **Step 3: Implement `ExtractDeviceName`**

Same pattern as `ExtractPinCode`: loop `arguments`, match `StartsWith('n') && Length > 1`, `Substring(1)`.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter UnitTest_MediaCommandExtensions`
Expected: PASS.

- [ ] **Step 5: Write failing tests for `BlinkLibrary.Snap`**

```csharp
[Fact] public void Test_SnapFailedNotLoggedIn()
[Fact] public void Test_SnapFailedNoDevicesLoaded()   // logged in, _sessionDetails.Cameras still null
[Fact] public void Test_SnapFailedDeviceNotFound()
[Fact] public void Test_SnapSuccessful_Camera()
[Fact] public void Test_SnapTimedOut()
```
`Test_SnapSuccessful_Camera` logs in, runs `Map()` (to populate the cache from
`MockData.CreateValidHomeScreenResponse()`), then `Snap(new[] { "nPergoda" })`
against a `MockHttpClientApiHandler` extended (Step 7) with the POST/GET-command
URLs for camera id `321343`/network `263178`; expect
`"New image captured for 'Pergoda'. Run IMAGE -nPergoda to download it."` and
that a second `Map()`-less `_sessionDetails` snapshot shows the home screen was
re-fetched (assert via a call-count or a changed `Thumbnail` value in the mock's
second `homescreen` response). `Test_SnapTimedOut` points the mock's command URL
at a permanently-incomplete response and expects
`"Blink did not finish capturing a new image for 'Pergoda' in time."` — inject
a near-zero poll interval so this doesn't take two minutes (see Interfaces: add
an optional `pollInterval`/`maxRetries` passthrough on `BlinkLibrary.Snap` only
if needed for the test to run fast, otherwise accept the real two-minute
ceiling is never hit by a correctly-mocked "already complete" response in the
non-timeout tests and only this one test pays the cost — implementer's call,
ledger it either way).

- [ ] **Step 6: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `Snap` does not exist.

- [ ] **Step 7: Implement the `IApiTransactions`/`ApiTransactions`/`AdapterExtension` additions, and extend `MockHttpClientApiHandler`**

Add the two trigger methods per the Interfaces block; add matching
`case` branches to `MockHttpClientApiHandler.Post`/`Get` for the URLs the new
tests exercise.

- [ ] **Step 8: Implement `BlinkLibrary.Snap`**

Guard clauses in this order: not logged in → `"Not currently logged into Blink service."`;
`_sessionDetails.Cameras is null && _sessionDetails.Doorbells is null` → `"Run MAP or GET first to load your devices."`;
name not found (both lookups `null`) → `"No single device named '<name>' was found. Run MAP to see exact names."`;
otherwise call the matching trigger method, re-fetch the home screen on
success, return the success/timeout message from the spec.

- [ ] **Step 9: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS, all `Snap` facts; full suite still green.

- [ ] **Step 10: Commit**

```bash
git add BlinkCommon/Interfaces/IApiTransactions.cs BlinkCameraCall/BlinkApiTransactions.cs \
  BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCall/Extensions/MediaCommandExtensions.cs \
  BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs BlinkCameraCallUnitTests/Extensions/UnitTest_MediaCommandExtensions.cs
git commit -m "Add SNAP command to trigger a fresh camera/doorbell snapshot"
```

---

## Task 3: `DEVICE` — single-device detail view

**Files:**
- Create: `BlinkCameraCall/Extensions/DeviceDetailExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceDetailExtension.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)

**Interfaces:**
- Consumes (Task 1): `DeviceLookupExtension.*`. (Task 2): `MediaCommandExtensions.ExtractDeviceName`.
- Produces: `DeviceDetailExtension.ToDeviceDetailString(this IHomeScreenCamera camera, IHomeScreenNetwork network) : string`
  and `.ToDeviceDetailString(this IHomeScreenDoorbells doorbell, IHomeScreenNetwork network) : string`
  (pure formatting, no I/O — same philosophy as `DeviceMapExtension.ToDeviceMapString`);
  `BlinkLibrary.Device(string[] arguments) : string`.

- [ ] **Step 1: Write failing tests for `ToDeviceDetailString`**

```csharp
[Fact] public void Test_ToDeviceDetailString_Camera_FormatsAllFields()
[Fact] public void Test_ToDeviceDetailString_Doorbell_FormatsAllFields()
```
Assert the exact multi-line string from the spec's `DEVICE` section, built
against `MockData.CreateValidHomeScreenCameras()[0]` / its network.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter UnitTest_DeviceDetailExtension`
Expected: FAIL.

- [ ] **Step 3: Implement `DeviceDetailExtension`**

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter UnitTest_DeviceDetailExtension`
Expected: PASS.

- [ ] **Step 5: Write failing tests for `BlinkLibrary.Device`**

```csharp
[Fact] public void Test_DeviceFailedNotLoggedIn()
[Fact] public void Test_DeviceFailedNoDevicesLoaded()
[Fact] public void Test_DeviceFailedDeviceNotFound()
[Fact] public void Test_DeviceSuccessful_Camera()
[Fact] public void Test_DeviceSuccessful_Doorbell()
```

- [ ] **Step 6: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `Device` does not exist.

- [ ] **Step 7: Implement `BlinkLibrary.Device`**

Same guard-clause order as `Snap` (Task 2, Step 8), minus the "not found" case
needing an API call — this command never calls the network.

- [ ] **Step 8: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS; full suite green.

- [ ] **Step 9: Commit**

```bash
git add BlinkCameraCall/Extensions/DeviceDetailExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceDetailExtension.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Add DEVICE command for single-device detail view"
```

---

## Task 4: `IMAGE` — download the current thumbnail

**Files:**
- Create: `BlinkCameraCall/Extensions/ThumbnailUrlExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_ThumbnailUrlExtension.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)

**Interfaces:**
- Consumes (Task 1): `DeviceLookupExtension.*`, `AdapterExtension.DownloadBytes`,
  `MediaFileExtension.SaveMediaFile`. (Task 2): `ExtractDeviceName`.
- Produces: `ThumbnailUrlExtension.BuildThumbnailUrl(string thumbnailField, string tier, long accountId, long networkId, long deviceId, bool isDoorbell) : string`
  (pure function implementing the spec's `IMAGE` section's two-case branching:
  integer `ts` → fixed-shape URL; otherwise append `.jpg` unless already ending
  `&ext=`); `BlinkLibrary.Image(string[] arguments) : string`.

- [ ] **Step 1: Write failing tests for `BuildThumbnailUrl`**

```csharp
[Fact] public void Test_BuildThumbnailUrl_NumericTimestamp_BuildsFixedShapeUrl()
[Fact] public void Test_BuildThumbnailUrl_OldApiPath_AppendsJpg()
[Fact] public void Test_BuildThumbnailUrl_AlreadyEndsWithExt_UsedAsIs()
[Fact] public void Test_BuildThumbnailUrl_Doorbell_UsesDoorbellSegment()
```
Exact expected URLs copied from the spec's `IMAGE` section (substituting this
task's test ids/tier).

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter UnitTest_ThumbnailUrlExtension`
Expected: FAIL.

- [ ] **Step 3: Implement `BuildThumbnailUrl`**

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter UnitTest_ThumbnailUrlExtension`
Expected: PASS.

- [ ] **Step 5: Write failing tests for `BlinkLibrary.Image`**

```csharp
[Fact] public void Test_ImageFailedNotLoggedIn()
[Fact] public void Test_ImageFailedNoDevicesLoaded()
[Fact] public void Test_ImageFailedDeviceNotFound()
[Fact] public void Test_ImageFailedNoThumbnailYet()   // empty Thumbnail field
[Fact] public void Test_ImageSuccessful()
```
`Test_ImageSuccessful` points `BlinkLibrary.Image` at a scratch `mediaDirectory`
(this task's one deviation from `MediaFileExtension`'s default path — expose an
optional constructor/field on `BlinkLibrary` purely for this test, or inject via
a settings-style hook; implementer's call, ledger it) and asserts the returned
message names a path that actually exists on disk with the mocked bytes.

- [ ] **Step 6: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `Image` does not exist.

- [ ] **Step 7: Implement `BlinkLibrary.Image`**, extending `MockHttpClientApiHandler.GetBytes` with the test's URL case.

- [ ] **Step 8: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS; full suite green.

- [ ] **Step 9: Commit**

```bash
git add BlinkCameraCall/Extensions/ThumbnailUrlExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_ThumbnailUrlExtension.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs
git commit -m "Add IMAGE command to download a device's current thumbnail"
```

---

## Task 5: `VIDEOS` and `DOWNLOAD` — cloud clips

**Files:**
- Create: `BlinkCommon/Interfaces/System/ICloudMediaItem.cs`
- Create: `BlinkCommon/Interfaces/System/IMediaChangedResponse.cs`
- Create: `BlinkCameraCall/Extensions/CloudMediaExtension.cs` (formatting +
  `-h` argument extraction + id lookup against a cached list)
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_CloudMediaExtension.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)

**Interfaces:**
- Consumes (Task 1): `AdapterExtension.DownloadBytes`, `MediaFileExtension.SaveMediaFile`.
- Produces: `ICloudMediaItem { long Id; long Network_Id; long Device_Id; string Device_Name; DateTime Created_At; string Media; bool Deleted; }`;
  `IMediaChangedResponse { List<ICloudMediaItem> Media; }`;
  `CloudMediaExtension.ExtractHours(this string[]? arguments) : int` (reads `-h`,
  defaults to `24` on missing/non-numeric — never throws);
  `CloudMediaExtension.ToCloudMediaListString(this List<ICloudMediaItem> items, int hours) : string`
  (numbered table per spec, or the "No cloud video events..." message for an
  empty list); `CloudMediaExtension.ExtractMediaId(this string[]? arguments) : long`
  (reads `-i`; throws `ArgumentException` if missing/non-numeric — `DOWNLOAD`
  and `LOCALDOWNLOAD` both use this, unlike `-h` which never throws);
  `IApiTransactions.SystemGetRecentMedia(IAuthAccount account, DateTime sinceUtc) : IMediaChangedResponse?`;
  `BlinkLibrary.Videos(string[] arguments) : string`, `BlinkLibrary.Download(string[] arguments) : string`.
  `BlinkLibrary` gains a private `List<ICloudMediaItem>? _lastCloudMedia` field,
  set by `Videos` (to the non-deleted items) and read by `Download`.

- [ ] **Step 1: Write failing tests for `ExtractHours` and `ExtractMediaId`**

```csharp
[Fact] public void Test_ExtractHours_Present_ReturnsValue()
[Fact] public void Test_ExtractHours_Missing_DefaultsTo24()
[Fact] public void Test_ExtractHours_NonNumeric_DefaultsTo24()
[Fact] public void Test_ExtractMediaId_Present_ReturnsValue()
[Fact] public void Test_ExtractMediaId_Missing_Throws()
[Fact] public void Test_ExtractMediaId_NonNumeric_Throws()
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter UnitTest_CloudMediaExtension`
Expected: FAIL.

- [ ] **Step 3: Implement `ExtractHours`/`ExtractMediaId`**

- [ ] **Step 4: Run to verify pass; then write failing tests for `ToCloudMediaListString`**

```csharp
[Fact] public void Test_ToCloudMediaListString_NonEmptyList_PrintsNumberedTable()
[Fact] public void Test_ToCloudMediaListString_EmptyList_PrintsNoEventsMessage()
```

- [ ] **Step 5: Run to verify failure, implement, run to verify pass**

Run: `dotnet test --filter UnitTest_CloudMediaExtension` before and after
implementing. Expected: FAIL then PASS.

- [ ] **Step 6: Write failing tests for `BlinkLibrary.Videos`/`Download`**

```csharp
[Fact] public void Test_VideosFailedNotLoggedIn()
[Fact] public void Test_VideosSuccessful_PopulatesCache()
[Fact] public void Test_VideosSuccessful_NoEvents()
[Fact] public void Test_DownloadFailedNotLoggedIn()
[Fact] public void Test_DownloadFailedNoCachedList()        // Download before any Videos call
[Fact] public void Test_DownloadFailedIdNotInCachedList()
[Fact] public void Test_DownloadSuccessful()
```
Note `Videos` does not require a prior `MAP`/`GET` (it's account-wide, not
scoped to the cached device list) — only `"Not currently logged into Blink
service."` gates it; this is a deliberate asymmetry with `Snap`/`Device`/`Image`,
called out here so the implementer doesn't "fix" it into a Review Focus-driven
guard it was never meant to need.

- [ ] **Step 7: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `Videos`/`Download` do not exist.

- [ ] **Step 8: Implement `IApiTransactions.SystemGetRecentMedia`, `AdapterExtension` passthrough, `BlinkLibrary.Videos`/`Download`**, extending `MockHttpClientApiHandler.Get`/`GetBytes` with this task's URLs.

- [ ] **Step 9: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS; full suite green.

- [ ] **Step 10: Commit**

```bash
git add BlinkCommon/Interfaces/System/ICloudMediaItem.cs BlinkCommon/Interfaces/System/IMediaChangedResponse.cs \
  BlinkCameraCall/Extensions/CloudMediaExtension.cs BlinkCommon/Interfaces/IApiTransactions.cs \
  BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCallUnitTests/Extensions/UnitTest_CloudMediaExtension.cs \
  BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Add VIDEOS and DOWNLOAD commands for cloud video clips"
```

---

## Task 6: `LOCALCLIPS` — SD-card manifest listing

**Files:**
- Create: `BlinkCommon/Interfaces/System/IManifestRequestResponse.cs`
- Create: `BlinkCommon/Interfaces/System/ILocalStorageManifestResult.cs`
- Create: `BlinkCommon/Interfaces/System/ILocalStorageClip.cs`
- Create: `BlinkCameraCall/Driver/ManifestPolling.cs`
- Create: `BlinkCameraCall/Extensions/LocalStorageExtension.cs` (formatting)
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Test: `BlinkCameraCallUnitTests/Driver/UnitTest_ManifestPolling.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_LocalStorageExtension.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)

**Interfaces:**
- Consumes (Task 1): `DeviceLookupExtension.FindSyncModuleByName`. (Task 2): `ExtractDeviceName`.
- Produces: `IManifestRequestResponse { long Id; }`;
  `ILocalStorageManifestResult { long Manifest_Id; List<ILocalStorageClip> Clips; }`;
  `ILocalStorageClip { long Id; string Camera_Name; DateTime Created_At; long Size; }`;
  `ManifestPolling.WaitForManifestReady(IApiMethods api, string manifestStatusUrl, TimeSpan? pollInterval = null, int maxRetries = 120) : ILocalStorageManifestResult?`
  (same loop shape as `CommandPolling`, but succeeds when `Clips != null` rather
  than on a `Complete` flag; returns `null` on exhaustion);
  `LocalStorageExtension.ToLocalClipsListString(this List<ILocalStorageClip> clips, string syncModuleName) : string`;
  `IApiTransactions.SystemRequestLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId) : IManifestRequestResponse?`,
  `.SystemGetLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId, long manifestRequestId) : ILocalStorageManifestResult?`;
  `BlinkLibrary.LocalClips(string[] arguments) : string`.
  `BlinkLibrary` gains a private field caching `(long SyncModuleId, long ManifestId, List<ILocalStorageClip> Clips)? _lastLocalManifest`
  — **this exact tuple shape is what Task 7 consumes**, so if the implementer
  changes it, Task 7's brief must be updated to match (ledger the change).

- [ ] **Step 1: Write failing tests for `ManifestPolling`**

```csharp
[Fact] public void Test_WaitForManifestReady_ReadyOnFirstPoll()
[Fact] public void Test_WaitForManifestReady_ReadyAfterSeveralPolls()
[Fact] public void Test_WaitForManifestReady_TimesOutAfterMaxRetries_ReturnsNull()
```
Same hand-rolled `IApiMethods` double approach as `UnitTest_CommandPolling`
(Task 1) — first N calls return `{"id":7}` (no `clips` key), later calls return
`{"manifest_id":99,"clips":[{"id":1,"camera_name":"Pergoda","created_at":"...","size":12345}]}`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter UnitTest_ManifestPolling`
Expected: FAIL.

- [ ] **Step 3: Implement the three new interfaces and `ManifestPolling`**

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter UnitTest_ManifestPolling`
Expected: PASS.

- [ ] **Step 5: Write failing tests for `ToLocalClipsListString`**

```csharp
[Fact] public void Test_ToLocalClipsListString_NonEmptyList_PrintsNumberedTable()
[Fact] public void Test_ToLocalClipsListString_EmptyList_PrintsNoClipsMessage()
```

- [ ] **Step 6: Run to verify failure, implement, run to verify pass**

Run: `dotnet test --filter UnitTest_LocalStorageExtension` before and after.
Expected: FAIL then PASS.

- [ ] **Step 7: Write failing tests for `BlinkLibrary.LocalClips`**

```csharp
[Fact] public void Test_LocalClipsFailedNotLoggedIn()
[Fact] public void Test_LocalClipsFailedNoDevicesLoaded()
[Fact] public void Test_LocalClipsFailedSyncModuleNotFound()
[Fact] public void Test_LocalClipsFailedNotActive()     // Local_Storage_Status != "active"
[Fact] public void Test_LocalClipsSuccessful_PopulatesCache()
[Fact] public void Test_LocalClipsSuccessful_EmptyManifest()
[Fact] public void Test_LocalClipsTimedOut()
```
`MockData.CreateValidHomeScreenSyncModules()`'s first entry already has
`Local_Storage_Status = "active"` (id `390959`, network `263178`) — use it for
the success cases; the second (`Local_Storage_Status = "unavailable"`) for
`Test_LocalClipsFailedNotActive`.

- [ ] **Step 8: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `LocalClips` does not exist.

- [ ] **Step 9: Implement `IApiTransactions` additions and `BlinkLibrary.LocalClips`**, extending `MockHttpClientApiHandler.Post`/`Get` with this task's URLs.

- [ ] **Step 10: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS; full suite green.

- [ ] **Step 11: Commit**

```bash
git add BlinkCommon/Interfaces/System/IManifestRequestResponse.cs BlinkCommon/Interfaces/System/ILocalStorageManifestResult.cs \
  BlinkCommon/Interfaces/System/ILocalStorageClip.cs BlinkCameraCall/Driver/ManifestPolling.cs \
  BlinkCameraCall/Extensions/LocalStorageExtension.cs BlinkCommon/Interfaces/IApiTransactions.cs \
  BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCallUnitTests/Driver/UnitTest_ManifestPolling.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_LocalStorageExtension.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Add LOCALCLIPS command to list a sync module's SD-card manifest"
```

---

## Task 7: `LOCALDOWNLOAD` — download one SD-card clip

**Files:**
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)

**Interfaces:**
- Consumes (Task 1): `CommandPolling.WaitForCommandComplete`, `ICommandStatusResponse`,
  `DeviceLookupExtension.FindSyncModuleByName`, `AdapterExtension.DownloadBytes`,
  `MediaFileExtension.SaveMediaFile`. (Task 2): `ExtractDeviceName`. (Task 5):
  `ExtractMediaId`. (Task 6): the `_lastLocalManifest` tuple shape
  `(long SyncModuleId, long ManifestId, List<ILocalStorageClip> Clips)?`.
- Produces: `IApiTransactions.BuildLocalStorageClipUrl(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId) : string`
  (pure URL builder, no I/O — the fixed template from the spec's `LOCALDOWNLOAD`
  section); `.SystemPrepareLocalStorageClip(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId) : ICommandStatusResponse?`
  (`POST` the built URL, then poll it exactly like `SNAP` does); `BlinkLibrary.LocalDownload(string[] arguments) : string`.

- [ ] **Step 1: Write failing tests for `BuildLocalStorageClipUrl`**

```csharp
[Fact] public void Test_BuildLocalStorageClipUrl_BuildsExactTemplate()
```
One fact is enough — this is a pure string-interpolation function with no
branches.

- [ ] **Step 2: Run to verify failure, implement, run to verify pass**

Run: `dotnet test --filter BuildLocalStorageClipUrl` before and after.
Expected: FAIL then PASS.

- [ ] **Step 3: Write failing tests for `BlinkLibrary.LocalDownload`**

```csharp
[Fact] public void Test_LocalDownloadFailedNotLoggedIn()
[Fact] public void Test_LocalDownloadFailedSyncModuleNotFound()
[Fact] public void Test_LocalDownloadFailedNoCachedManifest()           // LocalDownload before any LocalClips call
[Fact] public void Test_LocalDownloadFailedSyncModuleMismatch()         // cached manifest is for a different sync module
[Fact] public void Test_LocalDownloadFailedClipIdNotInManifest()
[Fact] public void Test_LocalDownloadSuccessful()
[Fact] public void Test_LocalDownloadTimedOut()
```
`Test_LocalDownloadFailedSyncModuleMismatch` is this task's Review Focus
case: run `LocalClips` for sync module A, then call `LocalDownload` naming
sync module B (or A's id but after swapping `_lastLocalManifest` to a
different `SyncModuleId` via a second `LocalClips` call against a different
mocked sync module) — expect `"Run LOCALCLIPS -n<syncmodule> first."`, not a
download against the stale manifest.

- [ ] **Step 4: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `LocalDownload` does not exist.

- [ ] **Step 5: Implement `IApiTransactions.SystemPrepareLocalStorageClip`, `AdapterExtension` passthroughs, `BlinkLibrary.LocalDownload`**, extending `MockHttpClientApiHandler.Post`/`GetBytes` with this task's URLs (same URL used for both, per the spec).

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS; full suite green.

- [ ] **Step 7: Commit**

```bash
git add BlinkCommon/Interfaces/IApiTransactions.cs BlinkCameraCall/BlinkApiTransactions.cs \
  BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Add LOCALDOWNLOAD command to download one SD-card clip"
```

---

## Task 8: Wire up all seven commands and update documentation

**Files:**
- Modify: `BlinkCameraCall/Program.cs`
- Modify: `BlinkCameraCallUnitTests/Extensions/CommandProcessorExtension.cs`
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/DEVELOPER.md`

**Interfaces:**
- Consumes (Tasks 2–7): `BlinkLibrary.Snap`/`Device`/`Image`/`Videos`/`Download`/`LocalClips`/`LocalDownload`.

- [ ] **Step 1: Add the seven commands to `Program.cs`'s switch and `HELP` text**

```csharp
case "device": Console.WriteLine(blinkLibrary.Device(command.Arguments)); break;
case "snap": Console.WriteLine(blinkLibrary.Snap(command.Arguments)); break;
case "image": Console.WriteLine(blinkLibrary.Image(command.Arguments)); break;
case "videos": Console.WriteLine(blinkLibrary.Videos(command.Arguments)); break;
case "download": Console.WriteLine(blinkLibrary.Download(command.Arguments)); break;
case "localclips": Console.WriteLine(blinkLibrary.LocalClips(command.Arguments)); break;
case "localdownload": Console.WriteLine(blinkLibrary.LocalDownload(command.Arguments)); break;
```
Plus one `Console.WriteLine` help line per command, matching the existing
`MAP`/`GET` lines' style.

- [ ] **Step 2: Add all seven to `CommandProcessorExtension.ValidCommands`**

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: PASS — this task has no new unit tests of its own (`Program.cs`'s
`switch` has never been unit-tested; `ValidCommands` membership is covered by
whatever pattern `UnitTest_CommandProcessorExtension.cs` already uses for
`"map"` — extend it the same way for these seven, if that file asserts
membership per-command rather than only testing unknown-command rejection).

- [ ] **Step 4: Update `docs/USER_GUIDE.md`**

Add all seven commands to the commands table and intro paragraph; add one new
`##`-level section per command (mirroring the existing "Device map (`MAP`)"
section) covering syntax, what it does, and its specific error messages from
the spec's Error handling table; extend the example session.

- [ ] **Step 5: Update `docs/DEVELOPER.md`**

Add a `### 7.x The <COMMAND> command` subsection per command (mirroring the
existing `### 7.1 The MAP command`), and add bullets to "## 10. Known
limitations / honest gaps" for: Owls/Sirens/Chimes/Doorbell_Buttons still
unsupported by these commands; no liveview; no delete-after-download for
local-storage clips; the `media/changed` response shape being a
reconstruction pending live verification.

- [ ] **Step 6: Commit**

```bash
git add BlinkCameraCall/Program.cs BlinkCameraCallUnitTests/Extensions/CommandProcessorExtension.cs \
  docs/USER_GUIDE.md docs/DEVELOPER.md
git commit -m "Wire up DEVICE/SNAP/IMAGE/VIDEOS/DOWNLOAD/LOCALCLIPS/LOCALDOWNLOAD and document them"
```
