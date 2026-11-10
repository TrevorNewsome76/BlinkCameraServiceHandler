# Local-storage background watching, with adaptive polling intervals for both watchers

## Motivation

`WATCH`/`UNWATCH` only detects events that have reached Blink's cloud
(`EventWatchPolling` polls the same cloud media-changed feed `VIDEOS`
shows). A camera/doorbell whose Sync Module is local-storage-only never
uploads clips to the cloud, so a real motion/doorbell trigger on such a
device produces no alert at all — not a bug, but a real gap for anyone
running local-only storage.

This spec adds a second, independent background watcher, `LOCALWATCH`/
`UNLOCALWATCH`, that polls every Sync Module's local SD-card manifest the
same way `LOCALCLIPS` already does, and alerts on new clips it hasn't
reported before.

Both watchers' polling intervals also become self-tuning and persisted:
if a round of checking takes longer than the currently configured
interval, the interval grows automatically (and is reported), so a slow
account doesn't end up with overlapping/backed-up checks. `WATCH` and
`LOCALWATCH` use the exact same retuning math, via one shared helper, so
their behavior is consistent.

## Current state (for reference)

- `EventWatchPolling.Run` (`BlinkCameraCall/Driver/EventWatchPolling.cs`)
  polls `adapter.GetRecentMedia` on a fixed interval (default 20s, from
  `BlinkLibrary.Watch`'s `arguments.ExtractWatchIntervalSeconds()`), with
  an in-memory `HashSet<long> seenIds`. Not persisted anywhere.
- `BlinkLibrary.Watch`/`Unwatch`/`_watchCts` (`BlinkCameraCall/BlinkLibrary.cs:532-572`)
  start/stop one `Task.Run(() => EventWatchPolling.Run(...))`, wired to an
  `Action<string> onAlert` supplied by `Program.cs`.
- `LOCALCLIPS` (`BlinkLibrary.LocalClips`, `BlinkCameraCall/BlinkLibrary.cs:599-629`)
  is the one-shot pattern this reuses: finds a named sync module, calls
  `adapter.RequestLocalStorageManifest` then `adapter.GetLocalStorageManifest`
  (which polls `ManifestPolling.WaitForManifestReady`, default 1s interval,
  120 retries, **no cancellation support at all today**), and lists the
  returned `Clips`.
- `IBlinkSettings`/`SettingsFileExtension` (`BlinkCommon/Interfaces/IBlinkSettings.cs`,
  `BlinkCameraCall/Extensions/SettingsFileExtension.cs`) persist `Email`/
  `Password`/`BaseUrl`/`HardwareId` to `config.json` next to the exe, loaded
  at startup via `settings.Load($"{AppContext.BaseDirectory}")` in
  `Program.cs`.
- `ILocalStorageClip` (`BlinkCommon/Interfaces/System/ILocalStorageClip.cs`):
  `long Id`, `string Camera_Name`, `DateTime Created_At`, `long Size`.
- `IHomeScreenSyncModule`: `long Id`, `string Name`, `string Network_Id`
  (string, needs `long.Parse`), `string Local_Storage_Status` (`"active"`
  when usable, matching `LocalClips`'s existing check).

## New setting: persisted, adaptive polling intervals

Add to `IBlinkSettings`:

```csharp
int WatchIntervalSeconds { get; set; }
int LocalWatchIntervalSeconds { get; set; }
```

Defaults when missing from `config.json` (new field on an existing file,
or a fresh file): `WatchIntervalSeconds = 20` (matches today's hardcoded
cloud-watch default), `LocalWatchIntervalSeconds = 5` (the agreed default
for local-storage watching). `SettingsFileExtension.Load`'s anonymous
default object and its post-deserialize assignment both get the two new
fields, each defaulting to its value above when the loaded value is `0`
(covers both a missing field in an old `config.json` and a freshly
constructed settings object). `SettingsFileExtension.Save` needs no
change — `Serialize()` already round-trips every property on the
interface.

## Shared retuning logic: `AdaptivePollInterval`

New file `BlinkCameraCall/Driver/AdaptivePollInterval.cs`:

```csharp
namespace BlinkCameraCall.Driver;

public sealed class AdaptivePollInterval(TimeSpan initial)
{
    public TimeSpan Current { get; private set; } = initial;

    // totalRoundElapsed: the full round's own wall-clock time (one cloud
    // check for WATCH; the sum of every sync module's check for LOCALWATCH).
    // slowestItemElapsed: the slowest single item's own time within the
    // round (equal to totalRoundElapsed when a round is only one item,
    // which is always true for WATCH).
    // Returns the new interval if the round was slower than Current (and
    // updates Current to it); returns null and leaves Current unchanged
    // otherwise.
    public TimeSpan? ReportRound(TimeSpan totalRoundElapsed, TimeSpan slowestItemElapsed)
    {
        if (totalRoundElapsed <= Current)
            return null;

        Current = slowestItemElapsed + TimeSpan.FromSeconds(1);
        return Current;
    }
}
```

This is the one piece of retuning math both watchers call, so they can
never drift apart. A round's own elapsed time is always measured by the
caller with `System.Diagnostics.Stopwatch`, never inside this class (it
has no knowledge of what a "round" does).

## `ManifestPolling.WaitForManifestReady` gains cancellation support

`BlinkCameraCall/Driver/ManifestPolling.cs` currently has no
`CancellationToken` parameter at all — a background local-storage watcher
needs one so `UNLOCALWATCH` can interrupt an in-progress manifest wait,
not just stop future rounds. Add a trailing optional parameter, checked
before the `Get` call and before the wait, using the same cancellable-wait
pattern already established for the busy-retry backoff in
`BlinkApiTransactions.TriggerSnapshot`:

```csharp
public static ILocalStorageManifestResult? WaitForManifestReady(
    IApiMethods api,
    string manifestStatusUrl,
    TimeSpan? pollInterval = null,
    int maxRetries = 120,
    CancellationToken cancellationToken = default)
{
    var interval = pollInterval ?? TimeSpan.FromSeconds(1);

    for (var attempt = 0; attempt < maxRetries; attempt++)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = (api.Get(manifestStatusUrl) ?? string.Empty).Deserialize<ILocalStorageManifestResult>();

        if (result.Clips is not null)
            return result;

        if (cancellationToken.WaitHandle.WaitOne(interval))
            cancellationToken.ThrowIfCancellationRequested();
    }

    return null;
}
```

This is a trailing, optional, additive parameter — every existing call
site (`SystemGetLocalStorageManifest`, and every test) keeps compiling
and behaving identically when it isn't supplied.

Thread a matching trailing `CancellationToken cancellationToken = default`
through, unchanged in every other respect:
- `IApiTransactions.SystemGetLocalStorageManifest` (interface)
- `BlinkApiTransactions.SystemGetLocalStorageManifest` (implementation —
  passes it straight into `ManifestPolling.WaitForManifestReady`)
- `AdapterExtension.GetLocalStorageManifest` (pass-through)

`SystemRequestLocalStorageManifest`/`RequestLocalStorageManifest` (the
manifest *request* POST itself) are **not** touched — they're a single,
fast POST with no wait loop, nothing to cancel mid-call, exactly like the
existing ARM/DISARM precedent of only adding cancellation where there's
an actual wait to interrupt.

## New background loop: `LocalStorageWatchPolling`

New file `BlinkCameraCall/Driver/LocalStorageWatchPolling.cs`, parallel to
`EventWatchPolling` but iterating a list of sync modules each round
instead of a single cloud call:

```csharp
public static class LocalStorageWatchPolling
{
    public static void Run(
        IAdapter adapter,
        IAuthAccount account,
        IReadOnlyList<IHomeScreenSyncModule> syncModules,
        Action<ILocalStorageClip, string syncModuleName> onNewClip,
        Action<Exception, string syncModuleName> onError,
        Action<TimeSpan, TimeSpan, TimeSpan> onIntervalTooShort, // (totalRoundElapsed, slowestItemElapsed, newInterval)
        Action<TimeSpan> onIntervalPersisted,
        AdaptivePollInterval interval,
        CancellationToken token)
}
```

Behavior per round (one round = one pass over every entry in
`syncModules`, in list order — never parallel, per the agreed design):

1. For each sync module: check `token.IsCancellationRequested` before
   starting it (consistent with `ArmOrDisarmNetwork`'s per-item
   cancellation checks) — if cancelled, stop the round immediately.
2. Time that module's check with a `Stopwatch`: `adapter.RequestLocalStorageManifest`
   then `adapter.GetLocalStorageManifest(..., cancellationToken: token)`.
   A `null` manifest result (timed out server-side) is not an error — just
   produces no clips to report for that module this round, same as
   `LocalClips`'s own `if (manifestResult is null)` handling.
3. Track seen clip ids **per sync module** (`Dictionary<long, HashSet<long>>`,
   keyed by `syncModule.Id`) — a clip id seen on one module is unrelated
   to the same id on another. For every clip in the result not yet in
   that module's seen set: add it, then `onNewClip(clip, syncModule.Name)`.
4. Catch any exception for that one module's check (`OperationCanceledException`
   first, rethrown so it propagates and stops the round — don't swallow a
   real cancel as a per-item failure, mirroring the ARM/DISARM catch-arm
   ordering fix; then a generic `catch (Exception ex)` calling
   `onError(ex, syncModule.Name)`) and continue to the next module —
   never let one module's failure stop the round.
5. After the round finishes every module (or is cut short by
   cancellation — a cancelled round does **not** call `ReportRound`, since
   a partial round's timing isn't meaningful): call
   `interval.ReportRound(totalRoundElapsed, slowestModuleElapsed)`. If it
   returns a new interval, call `onIntervalTooShort(totalRoundElapsed,
   slowestModuleElapsed, newInterval)` then `onIntervalPersisted(newInterval)`.
6. Cancellable sleep for `interval.Current` (the same
   `token.WaitHandle.WaitOne(...)` + `ThrowIfCancellationRequested()`
   pattern as the busy-retry fix) before the next round.
7. Loop until `token.IsCancellationRequested`.

If `syncModules` is empty, `Run` returns immediately without looping (the
caller — `BlinkLibrary.LocalWatch` — is expected to catch the
zero-sync-modules case before ever starting the background task; `Run`
handling it too is just defensive, not the primary guard).

## `BlinkLibrary` changes

**`Watch` (existing method, `BlinkCameraCall/BlinkLibrary.cs:532-554`)**
gains adaptive retuning, consistent with `LocalWatch` below:

```csharp
public string Watch(string[] arguments, Action<string> onAlert, int persistedIntervalSeconds, Action<int> onIntervalPersisted)
```

- `persistedIntervalSeconds` is `settings.WatchIntervalSeconds`, passed in
  by `Program.cs` (loaded at startup). If `arguments` supplies `-s`, that
  value wins for this run **and** is immediately reported via
  `onIntervalPersisted` (so `Program.cs` saves it right away); otherwise
  `persistedIntervalSeconds` is the starting interval.
- Build `var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(startingSeconds));`
  and pass it into `EventWatchPolling.Run` (see below), along with two new
  callbacks wired to `onAlert`/`onIntervalPersisted`.
- Return message and the two existing guard clauses (not logged in,
  already watching) are unchanged.

**`EventWatchPolling.Run` (existing method)** gains the same shape of
parameters as `LocalStorageWatchPolling.Run` for the retuning piece —
`AdaptivePollInterval interval` replaces the current bare
`TimeSpan? pollInterval` parameter (the caller already resolved the
starting value into the `AdaptivePollInterval` it constructs), plus
`Action<TimeSpan, TimeSpan> onIntervalTooShort` (totalRoundElapsed is the
same value as slowestItemElapsed here, but both are still passed through,
matching `AdaptivePollInterval.ReportRound`'s signature — call it with the
same value twice) and `Action<TimeSpan> onIntervalPersisted`. Each round:
time the existing `adapter.GetRecentMedia` call with a `Stopwatch`, then
after it (success or caught-and-reported error) call
`interval.ReportRound(elapsed, elapsed)`; if non-null, call
`onIntervalTooShort(elapsed, elapsed, newInterval)` then
`onIntervalPersisted(newInterval)`; sleep for `interval.Current`
(cancellable, same `WaitHandle.WaitOne` pattern — replacing today's plain
`Thread.Sleep(interval)`, which is itself a related correctness fix: today
a cancel during the post-round sleep isn't honored until the full sleep
elapses, same latent issue as the busy-retry backoff had before its own
fix).

**New `LocalWatch`/`Unlocalwatch` pair**, mirroring `Watch`/`Unwatch`
exactly, with their own field:

```csharp
private CancellationTokenSource? _localWatchCts;

public string LocalWatch(string[] arguments, Action<string> onAlert, int persistedIntervalSeconds, Action<int> onIntervalPersisted)
{
    if (!(_sessionDetails?.LoggedInStatus ?? false))
        return "Not currently logged into Blink service.";

    if (_localWatchCts is not null)
        return "Already watching local storage. Use UNLOCALWATCH to stop.";

    var syncModules = (_sessionDetails.SyncModules ?? new List<IHomeScreenSyncModule>())
        .Where(s => s.Local_Storage_Status == "active")
        .ToList();

    if (syncModules.Count == 0)
        return "No sync modules with active local storage to watch.";

    // ...resolve starting interval from arguments/-s or persistedIntervalSeconds,
    // same pattern as Watch...

    var account = _sessionDetails.Account!;
    var cts = new CancellationTokenSource();
    _localWatchCts = cts;

    Task.Run(() => LocalStorageWatchPolling.Run(
        adapter,
        account,
        syncModules,
        (clip, syncModuleName) => onAlert($"[LOCAL ALERT] New clip: {clip.Camera_Name} on '{syncModuleName}' at {clip.Created_At:yyyy-MM-dd HH:mm:ss} UTC (id {clip.Id})"),
        (ex, syncModuleName) => onAlert($"[LOCALWATCH] Error checking '{syncModuleName}': {ex.Message}"),
        (total, slowest, newInterval) => onAlert($"[LOCALWATCH] Checking local storage took {total.TotalSeconds:0.0}s across {syncModules.Count} sync module(s) (slowest: {slowest.TotalSeconds:0.0}s) — longer than the current interval, extending it to {newInterval.TotalSeconds:0.0}s."),
        newSeconds => onIntervalPersisted((int)newSeconds.TotalSeconds),
        interval,
        cts.Token));

    return $"Watching local storage for new clips every {startingSeconds}s across {syncModules.Count} sync module(s). Use UNLOCALWATCH to stop.";
}

public string Unlocalwatch()
{
    if (_localWatchCts is null)
        return "Not currently watching local storage.";

    _localWatchCts.Cancel();
    _localWatchCts = null;
    return "Stopped watching local storage.";
}
```

`Logout` (`BlinkCameraCall/BlinkLibrary.cs:60-66`, which already calls
`StopWatch()`) also stops `_localWatchCts` the same way, so logging out
never leaves an orphaned local-storage watcher running — mirrors exactly
how it already handles the cloud watcher.

`WATCH`'s own retune alert message (new, for parity):
`"[WATCH] Checking for new events took {total}s — longer than the current interval, extending it to {new}s."`
(no "across N" clause — a cloud round is always exactly one check).

## `Program.cs` wiring

- `settings.Load(...)` already loads the whole `IBlinkSettings` object,
  so `settings.WatchIntervalSeconds`/`LocalWatchIntervalSeconds` are
  available with no extra call.
- `case "watch"` passes `settings.WatchIntervalSeconds` and an
  `onIntervalPersisted` callback that sets `settings.WatchIntervalSeconds`
  and calls `settings.Save($"{AppContext.BaseDirectory}")`, into
  `blinkLibrary.Watch(...)`. `TryAutoStartWatch` (used after login/verify)
  passes the same two things.
- New `case "localwatch"` / `case "unlocalwatch"`, same shape as
  `case "watch"`/`"unwatch"`, calling the new `BlinkLibrary` methods.
  `LOCALWATCH` is **not** auto-started after login (per the agreed
  design — it's an explicit, separate command).
- HELP text gains `LOCALWATCH -s<seconds>` / `UNLOCALWATCH` rows, same
  style as the existing `WATCH`/`UNWATCH` rows.

## Message/table additions for docs

`docs/USER_GUIDE.md`'s existing "Watching for new events (`WATCH`/
`UNWATCH`)" section gets a new subsection "Watching local storage
(`LOCALWATCH`/`UNLOCALWATCH`)" right after it, following the same
structure (example transcript, message table, notes) as the existing
section, plus one shared paragraph (referenced from both subsections)
explaining the adaptive-interval behavior and that it's persisted across
runs. `docs/DEVELOPER.md` gets a new `### 7.11` subsection documenting
`AdaptivePollInterval`, the `ManifestPolling` cancellation addition, and
the new background loop's shape.

## Explicitly out of scope

- Shrinking the interval back down if checks get faster later — only
  grows, per what was actually requested; no mechanism to re-tighten it.
- Any change to `VIDEOS`/`DOWNLOAD`/`LOCALCLIPS`/`LOCALDOWNLOAD`
  themselves — this only adds background watching alongside them.
- Auto-starting `LOCALWATCH` on login (explicitly declined — separate,
  manual command).
- Parallel/concurrent sync-module checking within a `LOCALWATCH` round
  (explicitly declined — sequential, one at a time).
- Any change to `HttpClientApiHandler`'s 10-second request timeout or the
  generic-`AggregateException`-message issue raised alongside this
  request — unrelated pre-existing behavior, not part of this feature.
