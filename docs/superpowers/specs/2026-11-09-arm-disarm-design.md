# Manual Arm/Disarm (system / hub / device) — Design Spec

## Context

The terminal can already read a hub's `Armed` flag and a camera/doorbell's
`Enabled` flag (both shown by `MAP`/`DEVICE`), but there is no command that
*changes* either one — arming/disarming has always had to be done from the
Blink app itself. The user wants to do this from the terminal, at three
levels: the whole account at once, one hub at a time, or one device at a
time, with the existing `cd`/`ls`/`trigger` navigation layer
([2026-11-08-shell-navigation-design.md](2026-11-08-shell-navigation-design.md))
as the way to say *which* level you mean.

**What "armed" actually means on Blink's side, and why that matters here:**
researched against blinkpy and community sources (not confirmed live — see
Non-goals), Blink's own API only has one true "armed" state, and it lives on
the **network** (hub): `POST /network/{id}/arm` / `/disarm`, the same
async-command shape `SNAP` already uses. Individual cameras/doorbells don't
have a second, independent "armed" flag — they have `Enabled`
(`IHomeScreenCamera`/`IHomeScreenDoorbells`, already in this codebase's model,
shown in `MAP` as `[enabled, ...]`/`[disabled, ...]`), toggled via
`POST /network/{id}/camera|doorbell/{id}/enable|disable`. A disabled camera
in an armed network still won't detect motion, and an enabled camera in a
disarmed network won't either — the two flags are independent in Blink's
real system. "Device-level arm/disarm" in this feature is therefore just
this project's name for toggling `Enabled`, not a third, separate state.

## Problem

There's no way to change either flag from this app at all — not the network
`Armed` flag, not a camera/doorbell's `Enabled` flag. Doing it from the
terminal also needs a sensible way to act at three different granularities
without inventing three sets of flags, and a way to let one hub/system-wide
command's outcome be "mostly successful" (some devices armed, one offline)
without that one failure blocking everything else or going unreported.

## Goals

- `ARM`/`DISARM`, run from wherever you're `cd`'d:
  - **At the root** — every hub, and every camera/doorbell under each of
    them.
  - **Inside a hub** — that one hub, and every camera/doorbell under it.
  - **Inside a device** — that one camera/doorbell's `Enabled` flag only.
- **Parent governs children**: arming/disarming a hub (standalone or as part
  of arming/disarming the whole system) also arms/disarms every camera and
  doorbell under it, so the displayed state is always consistent top to
  bottom — never "hub armed, camera still shows disabled" after running
  `ARM` at the hub/system level.
- Device-level `ARM`/`DISARM` only ever touches that one device — it never
  reaches up and changes its hub's own `Armed` flag.
- A hub/system-level operation keeps going even if one item fails (offline
  device, rejected request, timeout) and reports every item's own outcome
  plus a final success count, rather than aborting partway through or
  silently swallowing a partial failure.
- Both `MAP`'s `Armed`/`Enabled` display and this feature's own output stay
  accurate immediately afterward — a hub/system/device operation refreshes
  the cached home-screen data once it's done, the same way `SNAP` already
  does.

## Non-goals / known risks

- **No flag-style equivalent** (unlike every other device command, which has
  both a `cd`-context form and a flag form, e.g. `TRIGGER` / `SNAP
  -n<name>`). `ARM`/`DISARM` take no arguments at all — you must `cd` to the
  level you want first. This was a deliberate choice (see the three-way
  design discussion this spec followed) to avoid inventing a second flag
  letter to disambiguate a hub name from a device name.
- **The three new endpoint families
  (`/network/{id}/arm`/`/disarm`, `/network/{id}/camera/{id}/enable`/`/disable`,
  `/network/{id}/doorbells/{id}/enable`/`/disable`) are a best-effort
  reconstruction, not confirmed against a live account** — the same honesty
  already given to `VIDEOS`/`IMAGE`/`DOWNLOAD` in
  [2026-11-03-device-detail-and-media-design.md](2026-11-03-device-detail-and-media-design.md).
  Expect the same kind of live-error-driven field/path fix those needed, if
  Blink's real shape differs. Every failure path here still surfaces
  Blink's raw response for exactly that reason.
- **Owls (Blink Mini cameras), Sirens, Chimes, and Doorbell_Buttons are not
  touched.** They're still unmodeled raw `string[]` placeholders (per the
  device-map spec's own non-goals) with no id/name/`Enabled` to call
  enable/disable against, so a hub/system-level arm/disarm silently skips
  them — consistent with `MAP` already showing them only as a bare count.
- **No confirmation prompt before disarming.** `DISARM` takes effect
  immediately, the same way every other state-changing command in this app
  already does (`SNAP`, `LOGOUT`, etc. don't ask "are you sure?" either).

## Design

### API layer (`IApiTransactions` / `BlinkApiTransactions`)

Six new methods, all built on the existing private `TriggerSnapshot` helper
(unchanged — it already does exactly "POST this URL with no body, extract a
command id, poll `/network/{id}/command/{id}` until done," which is exactly
what arm/disarm/enable/disable also need):

```csharp
ICommandStatusResponse? SystemArmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? SystemDisarmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? SystemEnableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? SystemDisableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? SystemEnableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? SystemDisableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
```

URLs (each just a new call into the existing `TriggerSnapshot(triggerUrl, networkId, tier, pollInterval, maxRetries)`):

| Method | URL |
|---|---|
| `SystemArmNetwork` | `https://rest-{tier}.immedia-semi.com/network/{networkId}/arm` |
| `SystemDisarmNetwork` | `https://rest-{tier}.immedia-semi.com/network/{networkId}/disarm` |
| `SystemEnableCamera` | `https://rest-{tier}.immedia-semi.com/network/{networkId}/camera/{cameraId}/enable` |
| `SystemDisableCamera` | `https://rest-{tier}.immedia-semi.com/network/{networkId}/camera/{cameraId}/disable` |
| `SystemEnableDoorbell` | `https://rest-{tier}.immedia-semi.com/api/v1/accounts/{account.Account_Id}/networks/{networkId}/doorbells/{doorbellId}/enable` |
| `SystemDisableDoorbell` | `https://rest-{tier}.immedia-semi.com/api/v1/accounts/{account.Account_Id}/networks/{networkId}/doorbells/{doorbellId}/disable` |

(Camera URLs mirror `SystemTriggerCameraSnapshot`'s existing old-style path;
doorbell URLs mirror `SystemTriggerDoorbellSnapshot`'s existing
`/api/v1/accounts/...` path — each endpoint matches the URL *family* its
existing snapshot-trigger sibling already uses in this codebase, just with a
different final segment and no body.)

`AdapterExtension` gets a matching pass-through for each (dropping the
`System` prefix, same as every existing entry):

```csharp
ICommandStatusResponse? ArmNetwork(this IAdapter adapter, IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? DisarmNetwork(this IAdapter adapter, IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? EnableCamera(this IAdapter adapter, IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? DisableCamera(this IAdapter adapter, IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? EnableDoorbell(this IAdapter adapter, IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
ICommandStatusResponse? DisableDoorbell(this IAdapter adapter, IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
```

### Business layer (`BlinkLibrary`)

Three public overloads each for `Arm`/`Disarm`, mirroring the existing
`(name, camera, doorbell)` shape `ListDeviceMedia`/`DownloadCurrentThumbnail`
already use for device-scoped operations:

```csharp
public string Arm();                                                                 // system-wide
public string Arm(IHomeScreenNetwork network);                                        // one hub
public string Arm(string deviceName, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell); // one device

public string Disarm();
public string Disarm(IHomeScreenNetwork network);
public string Disarm(string deviceName, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell);
```

**Device-level** (`Arm`/`Disarm(name, camera, doorbell)`) — a single
enable/disable call, same guard/message shape `Snap` already uses:

- Not logged in → `"Not currently logged into Blink service."`
- `camera is null && doorbell is null` → `$"No single device named '{deviceName}' was found. Run MAP to see exact names."`
- Calls `adapter.EnableCamera`/`DisableCamera` or `EnableDoorbell`/`DisableDoorbell`
  depending on which of `camera`/`doorbell` is non-null (same
  camera-vs-doorbell branching `Snap` already does).
- `null` result (timed out) → `$"Blink did not finish arming '{deviceName}' in time."` (or `"disarming"`)
- An outright-rejected request still throws `InvalidOperationException`
  from the API layer and is **not** caught here — it propagates to
  `Program.cs`'s existing top-level catch, exactly like `Snap` today. There
  is only one item at this level, so there's nothing to "keep going" for.
- Success → refreshes the cached home-screen (see "Refreshing cached state"
  below) and returns `$"Armed '{deviceName}'."` / `$"Disarmed '{deviceName}'."`

**Hub-level** (`Arm`/`Disarm(network)`) and **system-level** (`Arm`/`Disarm()`)
share one private helper that arms/disarms a single network *and* cascades
to its children, catching each item's failure independently so one bad
device never stops the rest (the user's explicit "continue and report"
choice):

```csharp
private (List<string> Lines, int Succeeded, int Total) ArmOrDisarmNetwork(IHomeScreenNetwork network, bool arm)
```

For the network itself, then for every camera/doorbell whose `Network_Id`
matches it, in order:

1. Call the matching adapter method inside a `try`/`catch (Exception ex)` —
   deliberately broad, not just the `InvalidOperationException` an outright
   rejection throws, so a network blip or any other unexpected failure on
   one item still can't take down the rest of the loop (the whole point of
   "continue and report").
2. On success (non-null result) → `"armed."`/`"disarmed."` and count as a success.
3. On a `null` result (timeout) → `"Blink did not finish arming '<name>' in time."` (or disarming).
4. On a caught exception → `ex.Message` (for the expected rejection case
   this is already `"Blink rejected the request: <raw>"`; for anything else
   it's whatever that exception says, same as `Program.cs`'s existing
   top-level catch would have shown for a single-item command).
5. Append one line: the network's own line unindented
   (`"Hub '<name>': armed."`), each device's line indented two spaces
   (`"  Camera '<name>': armed."` / `"  Doorbell '<name>': armed."`).

**Hub-level public method** wraps this: logged-in guard, call the helper
once, append a closing summary line, refresh cached state, join with
`Environment.NewLine`:

```
Hub 'Home Front': armed.
  Camera 'Front Door': armed.
  Camera 'Garage': Blink did not finish arming 'Garage' in time.
  Doorbell 'Front door bell': armed.
Armed hub 'Home Front' (3/4 succeeded).
```

**System-level public method**: logged-in guard; `_sessionDetails.Networks is null`
→ `"Run MAP or GET first to load your devices."`; empty `Networks` →
`"No hubs to arm."` (or disarm); otherwise calls the same helper once per
network, concatenates every network's full block (including each one's own
`"Armed hub '<name>' (.../... succeeded)."` line — the exact same block the
standalone hub-level command would have printed for that hub), then appends
one grand-total line, refreshes cached state once at the very end (not per
hub):

```
Hub 'Home Front': armed.
  Camera 'Front Door': armed.
  Camera 'Garage': armed.
Armed hub 'Home Front' (3/3 succeeded).
Hub 'Home Back': armed.
Armed hub 'Home Back' (1/1 succeeded).
Armed the system (4/4 succeeded across 2 hubs).
```

**Refreshing cached state**: a new small private helper,

```csharp
private void RefreshDeviceLists()
{
    var response = adapter.GetHomeScreen(_sessionDetails.Account!);
    _sessionDetails.Networks = response?.Networks;
    _sessionDetails.SyncModules = response?.Sync_Modules;
    _sessionDetails.Cameras = response?.Cameras;
    _sessionDetails.Doorbells = response?.Doorbells;
}
```

This exact four-line block is already duplicated three times in
`BlinkLibrary` (`Get`, `Snap`, `Map`); rather than add a *fourth* and *fifth*
copy for `Arm`/`Disarm`, it's worth extracting now since this change is
already touching the file for a closely related reason. The three existing
call sites are left exactly as they are — only the two new ones use the
helper — this is a narrowly-scoped tidy-up riding along with the new
feature, not a general refactor of unrelated code.

### Shell layer (`ShellSession`, `Program.cs`, `CommandProcessorExtension`)

`ShellSession` gains two methods with zero new business logic — pure
context resolution, exactly like `Trigger()`:

```csharp
public string Arm()
{
    if (_currentDeviceName is not null)
    {
        var (camera, doorbell) = ResolveCurrentDevice();
        return library.Arm(_currentDeviceName, camera, doorbell);
    }

    return _currentNetwork is not null ? library.Arm(_currentNetwork) : library.Arm();
}
```

`Disarm()` is the same shape calling `library.Disarm(...)`. Neither needs
its own "not logged in"/"not loaded" messages — `BlinkLibrary` already
produces those, and unlike `Trigger`, there's no invalid context here: every
level (root, hub, device) is a legal target for `ARM`/`DISARM`.

`CommandProcessorExtension.ValidCommands` gains `"arm"`, `"disarm"` as plain
entries (not `BarePositionalCommands` — they take no argument at all, so the
existing `" -"`-splitting path already produces the right
`Command`-only object for them, same as `"map"`/`"logout"`/`"help"` today).

`Program.cs` adds:

```csharp
case "arm":
    Console.WriteLine(shellSession.Arm());
    break;
case "disarm":
    Console.WriteLine(shellSession.Disarm());
    break;
```

and `HELP` gains:

```
ARM                               Arms the current system/hub/device (cd to choose the level).
DISARM                            Disarms the current system/hub/device (cd to choose the level).
```

### Error handling summary

| Condition | Output |
|---|---|
| Not logged in, any level | `"Not currently logged into Blink service."` |
| System-level, `MAP`/`GET` never run | `"Run MAP or GET first to load your devices."` |
| System-level, zero hubs | `"No hubs to arm."` / `"No hubs to disarm."` |
| Device-level, device no longer resolves | `"No single device named '<name>' was found. Run MAP to see exact names."` |
| Device-level, Blink never confirms in time | `"Blink did not finish arming '<name>' in time."` (or disarming) |
| Device-level, Blink rejects outright | Propagates as `InvalidOperationException`, surfaced by `Program.cs`'s existing top-level catch — same as `SNAP` today. |
| Hub/system-level, one item times out | That item's own line: `"Blink did not finish arming '<name>' in time."` — the rest of the operation continues. |
| Hub/system-level, one item rejected outright | That item's own line: the caught exception's message — the rest of the operation continues. |

### Testing

- **`MockHttpClientApiHandler`** — six new canned URL branches (POST +
  matching `/network/{id}/command/{id}` GET), following the exact pattern
  already used for the camera/doorbell snapshot trigger fixtures, including
  at least one each of: immediate success, a command that never completes
  (timeout), and a POST that comes back with no usable id (outright
  rejection).
- **`UnitTest_ApiTransactions`** additions — one test per new method per
  outcome (success / timeout / rejection), mirroring the existing
  `SystemTriggerCameraSnapshot`/`SystemTriggerDoorbellSnapshot` tests.
- **`UnitTest_BlinkLibrary`** additions — device-level `Arm`/`Disarm` for a
  camera, for a doorbell, for an unresolvable name, and not-logged-in;
  hub-level `Arm`/`Disarm` using the existing multi-camera/multi-doorbell
  fixture (`CreateValidHomeScreenResponse`) to assert the per-item lines and
  the closing count, including a case where one child device's call fails
  and the rest still run and still get reported; system-level `Arm`/`Disarm`
  across the existing two-network fixture, including zero-hubs and
  not-loaded-yet.
- **`UnitTest_ShellSession`** additions — `Arm`/`Disarm` at root delegates to
  `library.Arm()`/`Disarm()`; inside a hub delegates with that network;
  inside a device delegates with the resolved camera/doorbell — mirroring
  the existing `Trigger` dispatch test, verified via the same `MockAdapter`
  fixture rather than a mock of `BlinkLibrary` itself (no mocking seam exists
  there, nor is one needed — these are thin delegation checks against real
  fixture data, same as the existing `Trigger` test).
- **`UnitTest_CommandProcessorExtension`** additions — `"arm"`/`"disarm"`
  parse as valid commands with no arguments, mirroring the `watch`/`unwatch`
  additions from the previous feature.
