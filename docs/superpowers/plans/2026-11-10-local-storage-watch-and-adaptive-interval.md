# Local-Storage Watching and Adaptive Poll Intervals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a second, independent background watcher (`LOCALWATCH`/`UNLOCALWATCH`) that alerts on new Sync Module SD-card clips, and make both it and the existing cloud `WATCH` self-tune their polling interval (persisted across runs) using one shared retuning helper.

**Architecture:** A new `AdaptivePollInterval` helper carries the retune math and is used by both watchers. `EventWatchPolling` (existing) and a new `LocalStorageWatchPolling` each time their own "round" of work and report it to that helper. `ManifestPolling` gains cancellation support so a local-storage round can actually be interrupted mid-wait. `BlinkLibrary` gains `LocalWatch`/`Unlocalwatch` alongside the existing `Watch`/`Unwatch`, and `IBlinkSettings` gains two persisted interval fields that `Program.cs` loads at startup and saves whenever either watcher reports a change.

**Tech Stack:** C#/.NET 10, xUnit, FluentAssertions, `Shadow.Quack` (construction-only, never `JsonProxy`), `Dependency.Shelf`.

**Spec:** docs/superpowers/specs/2026-11-10-local-storage-watch-and-adaptive-interval-design.md

## Global Constraints

- Every new parameter added to an *existing* public signature is **trailing** and optional, so every current positional call site (production and test) keeps compiling and behaving identically.
- `AdaptivePollInterval.ReportRound(totalRoundElapsed, slowestItemElapsed)` retunes only when `totalRoundElapsed > Current`; the new interval is always exactly `slowestItemElapsed + TimeSpan.FromSeconds(1)`. For `WATCH`, `totalRoundElapsed` and `slowestItemElapsed` are always the same value (one check per round).
- `LOCALWATCH` checks sync modules strictly in list order, one at a time — never parallel.
- `LOCALWATCH` is **not** auto-started on login (unlike `WATCH`, which already is) — it's an explicit, separate command.
- A `LOCALWATCH` round cut short by cancellation never calls `ReportRound` — a partial round's timing isn't meaningful.
- `ManifestPolling.WaitForManifestReady` and `SystemGetLocalStorageManifest`/`GetLocalStorageManifest` gain cancellation support; `SystemRequestLocalStorageManifest`/`RequestLocalStorageManifest` (the one-shot POST) do **not** — nothing there to interrupt mid-wait.
- Settings defaults: `WatchIntervalSeconds = 20`, `LocalWatchIntervalSeconds = 5`, applied whenever the stored/loaded value is `0` (covers both a brand-new settings object and an old `config.json` missing the field).
- All existing tests must keep passing (with mechanical signature-only updates where a task below changes an existing method's parameter list), with pristine output.

## Review Focus

- A `LOCALWATCH` round where one sync module throws a non-cancellation exception must still check every remaining sync module in that same round (continue-and-report) — Task 5.
- Cancelling `LOCALWATCH` while one sync module's manifest poll is in progress must stop immediately, not run that poll out to its full retry budget — Tasks 3 and 5.
- Seen-clip tracking must be keyed per sync module — a clip id already seen on one sync module must never suppress an alert for the same numeric id on a *different* sync module — Task 5.
- Loading an old `config.json` written before this feature (missing both new fields) must still succeed with the documented defaults, never throw and never silently leave an interval at `0` seconds — Task 1.
- Replacing `EventWatchPolling`'s plain `Thread.Sleep` with a cancellable wait (needed so `WATCH`'s own retuned interval can still be interrupted promptly) must not weaken the existing cancel-before-the-first-poll guarantee — Task 4.

---

## File Structure

- **Modify** `BlinkCommon/Interfaces/IBlinkSettings.cs` — two new properties.
- **Modify** `BlinkCameraCall/Extensions/SettingsFileExtension.cs` — load/save the two new properties with defaults.
- **Create** `BlinkCameraCall/Driver/AdaptivePollInterval.cs` — shared retune helper.
- **Modify** `BlinkCameraCall/Driver/ManifestPolling.cs` — cancellation support.
- **Modify** `BlinkCommon/Interfaces/IApiTransactions.cs`, `BlinkCameraCall/BlinkApiTransactions.cs`, `BlinkCameraCall/Extensions/AdapterExtension.cs` — thread a trailing `CancellationToken` through the local-storage-manifest *get* path only.
- **Modify** `BlinkCameraCall/Driver/EventWatchPolling.cs` — adaptive retuning, cancellable sleep.
- **Modify** `BlinkCameraCall/BlinkLibrary.cs` — `Watch` gains retuning params; new `LocalWatch`/`Unlocalwatch`/`_localWatchCts`; `Logout` stops both watchers.
- **Create** `BlinkCameraCall/Driver/LocalStorageWatchPolling.cs` — the new background loop.
- **Modify** `BlinkCameraCall/Program.cs` — settings load/persist wiring, two new console commands, HELP text.
- **Modify** `docs/USER_GUIDE.md`, `docs/DEVELOPER.md` — document everything above.

---

### Task 1: Persisted adaptive-interval settings

**Files:**
- Modify: `BlinkCommon/Interfaces/IBlinkSettings.cs`
- Modify: `BlinkCameraCall/Extensions/SettingsFileExtension.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_SettingsFileExtension.cs` (create if it doesn't already exist — check first; if settings load/save already has a test file, extend it)

**Interfaces:**
- Produces: `IBlinkSettings.WatchIntervalSeconds { get; set; }` (int), `IBlinkSettings.LocalWatchIntervalSeconds { get; set; }` (int). Every later task that reads/writes these uses exactly these names.

- [ ] **Step 1: Write the failing tests**

In the settings-load test file, add (reusing whatever existing helper the file already uses to write a temp `config.json` and call `.Load(path)` — read the existing tests in that file first; if none exist yet, write a minimal one following `SettingsFileExtension.Load`'s own shape: a temp directory, a `config.json` written by hand, then `Duck.Implement<IBlinkSettings>(new()).Load(path)`):

```csharp
[Fact]
public void Test_Load_FileMissingNewFields_DefaultsBothIntervals()
{
    // Write a config.json containing only Email/Password/BaseUrl/HardwareId (no interval fields),
    // then Load() it and assert WatchIntervalSeconds == 20 and LocalWatchIntervalSeconds == 5.
}

[Fact]
public void Test_Load_FileHasStoredIntervals_UsesStoredValues()
{
    // Write a config.json whose WatchIntervalSeconds is 45 and LocalWatchIntervalSeconds is 8,
    // Load() it, and assert both values round-trip exactly.
}

[Fact]
public void Test_SaveThenLoad_RoundTripsBothIntervals()
{
    // Build an IBlinkSettings with WatchIntervalSeconds = 30, LocalWatchIntervalSeconds = 10,
    // Save() it to a temp path, Load() a fresh instance from that same path, assert both match.
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "UnitTest_SettingsFileExtension"` (or the correct class name if extending an existing file)
Expected: FAIL — `IBlinkSettings` has no `WatchIntervalSeconds`/`LocalWatchIntervalSeconds` yet (compile error), or (once added to the interface but not yet wired in `Load`) wrong defaulted values.

- [ ] **Step 3: Add the two properties to `IBlinkSettings`**

```csharp
int WatchIntervalSeconds { get; set; }
int LocalWatchIntervalSeconds { get; set; }
```

- [ ] **Step 4: Wire defaults and load/save in `SettingsFileExtension`**

In `Load`'s anonymous default object, add `WatchIntervalSeconds = 20` and `LocalWatchIntervalSeconds = 5`. After deserializing `fileSettings` from an existing file, assign `settings.WatchIntervalSeconds = fileSettings.WatchIntervalSeconds == 0 ? 20 : fileSettings.WatchIntervalSeconds;` and the same pattern for `LocalWatchIntervalSeconds` defaulting to `5` — this covers both a brand-new settings object and an old file missing the field (both deserialize/default to `0`). `Save` needs no change (`Serialize()` already covers every interface property).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "UnitTest_SettingsFileExtension"`
Expected: PASS

- [ ] **Step 6: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass, pristine output.

```bash
git add BlinkCommon/Interfaces/IBlinkSettings.cs BlinkCameraCall/Extensions/SettingsFileExtension.cs BlinkCameraCallUnitTests/Extensions/UnitTest_SettingsFileExtension.cs
git commit -m "Add persisted, defaulted polling-interval settings for WATCH and LOCALWATCH"
```

---

### Task 2: `AdaptivePollInterval` shared retune helper

**Files:**
- Create: `BlinkCameraCall/Driver/AdaptivePollInterval.cs`
- Test: `BlinkCameraCallUnitTests/Driver/UnitTest_AdaptivePollInterval.cs`

**Interfaces:**
- Produces: `AdaptivePollInterval(TimeSpan initial)`, property `TimeSpan Current { get; }`, method `TimeSpan? ReportRound(TimeSpan totalRoundElapsed, TimeSpan slowestItemElapsed)`. Tasks 4 and 5 both construct and call this exactly as written here — no other members.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Test_ReportRound_FasterThanCurrent_ReturnsNullAndLeavesCurrentUnchanged()
{
    var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(20));

    var result = interval.ReportRound(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    result.Should().BeNull();
    interval.Current.Should().Be(TimeSpan.FromSeconds(20));
}

[Fact]
public void Test_ReportRound_ExactlyCurrent_ReturnsNull()
{
    // totalRoundElapsed == Current (not greater) must not retune — boundary check.
    var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(20));

    var result = interval.ReportRound(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

    result.Should().BeNull();
    interval.Current.Should().Be(TimeSpan.FromSeconds(20));
}

[Fact]
public void Test_ReportRound_SlowerThanCurrent_ReturnsSlowestItemPlusOneSecondAndUpdatesCurrent()
{
    var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(5));

    var result = interval.ReportRound(TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(12));

    result.Should().Be(TimeSpan.FromSeconds(13));
    interval.Current.Should().Be(TimeSpan.FromSeconds(13));
}

[Fact]
public void Test_ReportRound_SingleItemRound_TotalAndSlowestAreTheSameValue()
{
    // Mirrors how WATCH calls this (one check per round: both arguments are the same value).
    var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(20));

    var result = interval.ReportRound(TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25));

    result.Should().Be(TimeSpan.FromSeconds(26));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "UnitTest_AdaptivePollInterval"`
Expected: FAIL — `AdaptivePollInterval` doesn't exist yet.

- [ ] **Step 3: Implement `AdaptivePollInterval` in `BlinkCameraCall/Driver/AdaptivePollInterval.cs`**

Exactly the shape in the spec's "Shared retuning logic" section — a sealed class with a constructor taking the initial `TimeSpan`, a `Current` property, and `ReportRound` comparing `totalRoundElapsed` against `Current` (strictly greater triggers a retune), setting and returning `slowestItemElapsed + TimeSpan.FromSeconds(1)` when it does.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "UnitTest_AdaptivePollInterval"`
Expected: PASS

- [ ] **Step 5: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass, pristine output.

```bash
git add BlinkCameraCall/Driver/AdaptivePollInterval.cs BlinkCameraCallUnitTests/Driver/UnitTest_AdaptivePollInterval.cs
git commit -m "Add AdaptivePollInterval, the shared retune helper for WATCH and LOCALWATCH"
```

---

### Task 3: `ManifestPolling` cancellation support

**Files:**
- Modify: `BlinkCameraCall/Driver/ManifestPolling.cs`
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs` (one signature)
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs` (`SystemGetLocalStorageManifest`)
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs` (`GetLocalStorageManifest` pass-through)
- Modify: `BlinkCameraCallUnitTests/Driver/UnitTest_EventWatchPolling.cs`'s `FakeApiTransactions` and any other hand-written `IApiTransactions` test stub — mechanical, add the new trailing optional parameter with no behavior change (search the whole test project for `IApiTransactions` implementers first, so none are missed)
- Test: `BlinkCameraCallUnitTests/Driver/UnitTest_ManifestPolling.cs` (create if it doesn't exist; check first — `CommandPolling` likely has an existing test file you can mirror the shape of)

**Interfaces:**
- Produces: `ManifestPolling.WaitForManifestReady(IApiMethods, string, TimeSpan?, int, CancellationToken cancellationToken = default)`; `IApiTransactions.SystemGetLocalStorageManifest(IAuthAccount, long, long, long, TimeSpan?, int, CancellationToken cancellationToken = default)`; `AdapterExtension.GetLocalStorageManifest(this IAdapter, IAuthAccount, long, long, long, TimeSpan?, int, CancellationToken cancellationToken = default)`. Task 5 calls the adapter extension method with this exact trailing parameter.

- [ ] **Step 1: Write the failing tests**

Mirror whatever pattern an existing `CommandPolling`/busy-retry cancellation test in this codebase already uses for a deterministic mid-wait cancel (search for one before writing these from scratch):

```csharp
[Fact]
public void Test_WaitForManifestReady_AlreadyCancelled_ThrowsBeforeFirstGet()
{
    // A pre-cancelled token must throw OperationCanceledException without calling api.Get at all.
}

[Fact]
public void Test_WaitForManifestReady_CancelledDuringWait_StopsBeforeNextAttempt()
{
    // A fake IApiMethods whose Get always returns a manifest with no Clips yet (forcing the wait
    // loop to actually wait at least once); cancel the token from inside a hook on the first Get
    // call (or via a timer, whichever the existing cancellation-test pattern in this codebase
    // uses); assert OperationCanceledException is thrown and Get was called far fewer times than
    // maxRetries would otherwise allow.
}

[Fact]
public void Test_WaitForManifestReady_NotCancelled_StillReturnsResultOnceCompleteFlagAppears()
{
    // Unchanged existing behavior, just confirming the new trailing parameter's default doesn't
    // break the happy path: Clips appears on the 2nd Get call, result is returned.
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "UnitTest_ManifestPolling"`
Expected: FAIL — no `cancellationToken` parameter yet.

- [ ] **Step 3: Add cancellation to `ManifestPolling.WaitForManifestReady`**

Exact shape from the spec's "`ManifestPolling.WaitForManifestReady` gains cancellation support" section: `cancellationToken.ThrowIfCancellationRequested()` before each `Get`, and `if (cancellationToken.WaitHandle.WaitOne(interval)) cancellationToken.ThrowIfCancellationRequested();` in place of the current plain `Thread.Sleep(interval)`.

- [ ] **Step 4: Thread the trailing `CancellationToken` through the three call sites**

`IApiTransactions.SystemGetLocalStorageManifest`, `BlinkApiTransactions.SystemGetLocalStorageManifest` (passes it straight into `ManifestPolling.WaitForManifestReady`), `AdapterExtension.GetLocalStorageManifest`. Do **not** touch `SystemRequestLocalStorageManifest`/`RequestLocalStorageManifest` — no change there at all.

- [ ] **Step 5: Mechanically update every other `IApiTransactions` implementer**

Add the same trailing `CancellationToken cancellationToken = default` to every hand-written test stub implementing `IApiTransactions` (at minimum `UnitTest_EventWatchPolling.cs`'s `FakeApiTransactions` — search the whole test project to be sure none are missed), returning its existing value unchanged. No behavior change, compile-fix only.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "UnitTest_ManifestPolling"`
Expected: PASS

- [ ] **Step 7: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass, pristine output.

```bash
git add BlinkCameraCall/Driver/ManifestPolling.cs BlinkCommon/Interfaces/IApiTransactions.cs BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCallUnitTests/Driver/UnitTest_EventWatchPolling.cs BlinkCameraCallUnitTests/Driver/UnitTest_ManifestPolling.cs
git commit -m "Add cancellation support to ManifestPolling's wait loop"
```

---

### Task 4: `WATCH` adopts `AdaptivePollInterval`

**Files:**
- Modify: `BlinkCameraCall/Driver/EventWatchPolling.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs` (`Watch` method only)
- Modify: `BlinkCameraCallUnitTests/Driver/UnitTest_EventWatchPolling.cs`
- Modify: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (existing `Watch`/`Unwatch` tests — mechanical call-site updates only, see Step 5)

**Interfaces:**
- Consumes: `AdaptivePollInterval` (Task 2).
- Produces: `EventWatchPolling.Run(IAdapter, IAuthAccount, Action<ICloudMediaItem> onNewEvent, Action<Exception> onError, CancellationToken, AdaptivePollInterval interval, Action<TimeSpan, TimeSpan, TimeSpan>? onIntervalTooShort = null, Action<TimeSpan>? onIntervalPersisted = null, int maxIterations = int.MaxValue)` — note `interval` replaces the old `TimeSpan? pollInterval` positionally; `BlinkLibrary.Watch(string[] arguments, Action<string> onAlert, int persistedIntervalSeconds = 20, Action<int>? onIntervalPersisted = null)`. Task 7 calls `Watch` with real values for the two new trailing parameters.

- [ ] **Step 1: Update `UnitTest_EventWatchPolling.cs`'s existing calls to the new signature**

Every existing call currently passes `TimeSpan.Zero` positionally where `interval` now goes. Replace each with `new AdaptivePollInterval(TimeSpan.Zero)` (same test behavior — the fake transactions still return instantly, and `maxIterations` still bounds the loop the same way it always did). This is a mechanical update with no new assertions yet — run `dotnet test --filter "UnitTest_EventWatchPolling"` afterward and confirm these specific tests compile and fail only because `AdaptivePollInterval`/the new signature don't exist in production code yet (they were added in Task 2; `EventWatchPolling.Run` itself hasn't changed yet in this task, so this step should actually leave these tests RED only on the signature mismatch, not on missing types).

- [ ] **Step 2: Write the new failing tests for retuning**

Add to `UnitTest_EventWatchPolling.cs`:

```csharp
[Fact]
public void Test_Run_RoundSlowerThanInterval_CallsOnIntervalTooShortAndPersisted()
{
    // A FakeApiTransactions whose SystemGetRecentMedia sleeps briefly (e.g. Thread.Sleep(50ms))
    // before returning NoItems(), with an AdaptivePollInterval constructed with an initial value
    // small enough (e.g. TimeSpan.FromMilliseconds(10)) that the round is guaranteed slower.
    // Assert onIntervalTooShort and onIntervalPersisted are both invoked exactly once, with
    // onIntervalTooShort's three TimeSpan arguments all equal to each other (single-item round)
    // and onIntervalPersisted's argument equal to interval.Current after the call.
}

[Fact]
public void Test_Run_RoundFasterThanInterval_NeitherCallbackInvoked()
{
    // Normal-speed fake, large initial interval; assert neither callback fires.
}

[Fact]
public void Test_Run_CancelledDuringPostRoundWait_StopsPromptlyRatherThanWaitingFullInterval()
{
    // Construct an AdaptivePollInterval with a large interval (e.g. 30s), cancel the token from a
    // background Task.Delay(50ms)-triggered cts.Cancel() (or whatever deterministic pattern this
    // codebase's own prior cancellation tests already established), and assert the call returns
    // normally (Run never throws on cancellation — it just stops the loop) well under the 30s
    // interval — Stopwatch-timed, asserting elapsed is under e.g. 5s. This is the regression guard
    // for replacing Thread.Sleep with a cancellable wait.
}
```

Re-confirm `Test_Run_AlreadyCancelled_DoesNotPoll` (existing test) still encodes the right behavior with the new signature — it should need no logic change, only the mechanical `interval` argument swap from Step 1.

- [ ] **Step 3: Run tests to verify they fail for the right reason**

Run: `dotnet test --filter "UnitTest_EventWatchPolling"`
Expected: FAIL — `onIntervalTooShort`/`onIntervalPersisted` don't exist on `Run` yet; the cancellation test fails by timing out or not yet being cancellable.

- [ ] **Step 4: Implement the retuning and cancellable wait in `EventWatchPolling.Run`**

Time each round's `adapter.GetRecentMedia` call with `System.Diagnostics.Stopwatch`. After it (whether it succeeded or was caught and reported via `onError`), call `interval.ReportRound(elapsed, elapsed)`; if non-null, invoke `onIntervalTooShort?.Invoke(elapsed, elapsed, newInterval)` then `onIntervalPersisted?.Invoke(newInterval)`. Replace the trailing `Thread.Sleep(interval)` with:

```csharp
if (token.WaitHandle.WaitOne(interval.Current))
    break;
```

`Run`'s existing contract never throws on cancellation — it just stops the loop (confirmed by its current `for` condition and the `if (token.IsCancellationRequested) break;` check already ahead of the sleep). `WaitOne` returning `true` means cancellation fired during the wait, so `break` preserves that exact no-throw contract while making the wait itself interruptible instead of running out the full `interval.Current` first.

- [ ] **Step 5: Update `BlinkLibrary.Watch` and its existing tests**

Add the two new trailing parameters to `Watch`'s signature (`int persistedIntervalSeconds = 20, Action<int>? onIntervalPersisted = null`) exactly as specified in Global Constraints — every existing 2-argument call site in `UnitTest_BlinkLibrary.cs` keeps compiling unchanged. Inside `Watch`, resolve the starting interval (explicit `-s` from `arguments` wins and immediately calls `onIntervalPersisted`; otherwise `persistedIntervalSeconds`), construct `new AdaptivePollInterval(TimeSpan.FromSeconds(startingSeconds))`, and pass it plus two new lambdas into `EventWatchPolling.Run`: one wrapping `onAlert` with the `"[WATCH] Checking for new events took {total.TotalSeconds:0.0}s — longer than the current interval, extending it to {newInterval.TotalSeconds:0.0}s."` message, one forwarding to the `onIntervalPersisted` parameter.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "UnitTest_EventWatchPolling|UnitTest_BlinkLibrary"`
Expected: PASS

- [ ] **Step 7: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass, pristine output.

```bash
git add BlinkCameraCall/Driver/EventWatchPolling.cs BlinkCameraCall/BlinkLibrary.cs BlinkCameraCallUnitTests/Driver/UnitTest_EventWatchPolling.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Make WATCH's polling interval adaptive and persisted, with a cancellable wait"
```

---

### Task 5: `LocalStorageWatchPolling` — the new background loop

**Files:**
- Create: `BlinkCameraCall/Driver/LocalStorageWatchPolling.cs`
- Test: `BlinkCameraCallUnitTests/Driver/UnitTest_LocalStorageWatchPolling.cs`

**Interfaces:**
- Consumes: `AdaptivePollInterval` (Task 2), `ManifestPolling`'s now-cancellable `GetLocalStorageManifest` (Task 3).
- Produces: `LocalStorageWatchPolling.Run(IAdapter adapter, IAuthAccount account, IReadOnlyList<IHomeScreenSyncModule> syncModules, Action<ILocalStorageClip, string> onNewClip, Action<Exception, string> onError, Action<TimeSpan, TimeSpan, TimeSpan> onIntervalTooShort, Action<TimeSpan> onIntervalPersisted, AdaptivePollInterval interval, CancellationToken token, int maxIterations = int.MaxValue)`. The `maxIterations` trailing parameter exists purely so tests can bound the loop, mirroring `EventWatchPolling.Run`'s own existing parameter. Task 6 calls this exactly as written here.

- [ ] **Step 1: Write the failing tests**

Build a small fake `IAdapter`/`IApiTransactions` pair for this file only (don't reuse `UnitTest_EventWatchPolling.cs`'s `FakeApiTransactions` — this needs `SystemRequestLocalStorageManifest`/`SystemGetLocalStorageManifest` to return controllable, per-sync-module manifests, which that fixture doesn't support). Use `Dependency.Shelf.ShelveInstance<IApiTransactions>(...)` the same way the existing test file does.

```csharp
[Fact]
public void Test_Run_MultipleModules_DetectsNewClipsOnEachInListOrder()
{
    // Two sync modules, each with one clip Blink hasn't shown before; assert onNewClip fires
    // twice, in the same order as syncModules, each with the right syncModuleName.
}

[Fact]
public void Test_Run_RepeatedRound_DoesNotReportSameClipTwice()
{
    // maxIterations: 2, same manifest both rounds; onNewClip fires once per clip total, not twice.
}

[Fact]
public void Test_Run_SameClipIdOnDifferentModules_BothReported()
{
    // Two sync modules whose manifests both contain a clip with the SAME numeric Id; assert
    // onNewClip fires for both — proves seen-tracking is keyed per sync module, not globally.
}

[Fact]
public void Test_Run_OneModuleThrows_StillChecksTheRemainingModulesInThatRound()
{
    // Three sync modules; the middle one's manifest request throws a non-cancellation exception;
    // assert onError fires once for it, and onNewClip still fires for the third module's new clip
    // in the same round (continue-and-report, mirrors ARM/DISARM's per-item catch).
}

[Fact]
public void Test_Run_CancelledMidManifestPoll_StopsImmediately()
{
    // One sync module whose manifest never completes (forcing WaitForManifestReady to actually
    // wait); cancel the token from inside the fake's Get hook (mirroring whatever deterministic
    // cancel-mid-wait pattern Task 3's own tests established); assert the loop stops without
    // running out the full retry budget, and no partial-round ReportRound call occurred (assert
    // onIntervalTooShort/onIntervalPersisted never fire for this cancelled round).
}

[Fact]
public void Test_Run_RoundSlowerThanInterval_RetunesToSlowestModulePlusOneSecond()
{
    // Three sync modules; make one of them (via a small deliberate delay in the fake) clearly the
    // slowest; AdaptivePollInterval constructed small enough that the round is guaranteed slower;
    // assert onIntervalTooShort's three arguments are (totalRoundElapsed, thatSlowestModule'sOwn
    // elapsed, slowestModuleElapsed + 1s) and onIntervalPersisted receives the same new interval.
}

[Fact]
public void Test_Run_EmptySyncModuleList_ReturnsImmediatelyWithoutLooping()
{
    // syncModules: empty list; assert no exception, and none of the callbacks fire.
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "UnitTest_LocalStorageWatchPolling"`
Expected: FAIL — `LocalStorageWatchPolling` doesn't exist yet.

- [ ] **Step 3: Implement `LocalStorageWatchPolling.Run`**

Exact shape from the spec's "New background loop: `LocalStorageWatchPolling`" section: per round, iterate `syncModules` in order; for each, check `token.IsCancellationRequested` first and stop the round if set; time the request-then-wait pair with `Stopwatch`; treat a `null` manifest result (timed out server-side) as zero new clips, not an error; track seen clip ids in a `Dictionary<long, HashSet<long>>` keyed by `syncModule.Id`; catch `OperationCanceledException` first and let it propagate out of `Run` entirely (stopping the whole round and the whole loop — don't call `ReportRound` for a round that didn't finish); catch a generic `Exception` per module and call `onError`, then continue to the next module. After a round completes in full (not cancelled), call `interval.ReportRound(totalRoundElapsed, slowestModuleElapsed)`, invoking the two callbacks if it returns non-null. Sleep via the same cancellable-wait pattern as Task 4 before the next round. Honor `maxIterations` the same way `EventWatchPolling.Run` does.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "UnitTest_LocalStorageWatchPolling"`
Expected: PASS

- [ ] **Step 5: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass, pristine output.

```bash
git add BlinkCameraCall/Driver/LocalStorageWatchPolling.cs BlinkCameraCallUnitTests/Driver/UnitTest_LocalStorageWatchPolling.cs
git commit -m "Add LocalStorageWatchPolling, the background loop behind LOCALWATCH"
```

---

### Task 6: `BlinkLibrary.LocalWatch`/`Unlocalwatch`

**Files:**
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Consumes: `LocalStorageWatchPolling.Run` (Task 5).
- Produces: `BlinkLibrary.LocalWatch(string[] arguments, Action<string> onAlert, int persistedIntervalSeconds = 5, Action<int>? onIntervalPersisted = null) : string`; `BlinkLibrary.Unlocalwatch() : string`. Task 7 calls both exactly as written here.

- [ ] **Step 1: Write the failing tests**

Reuse the existing `MockAdapter(MockSettings.CreateNoThumbnailSettings())` + `unothumbnail@email.com`/`pPAssword` login fixture already used by the `LocalClips` tests (`UnitTest_BlinkLibrary.cs` around line 1139) — after `Login` + `Map`, its session already has sync modules `Active Sync` (id 500, active, has a clip), `Inactive Sync` (id 501, unavailable — must be excluded), `Empty Sync` (id 502, active, empty manifest), `Timeout Sync` (id 503), `Rejected Manifest Sync` (id 505), `Reject Prepare Sync` (id 506) — read `MockData.cs` around line 597-603 and the matching `MockClientApiHandler`/adapter fixtures for each before writing assertions, so expected clip ids/names are accurate rather than guessed.

```csharp
[Fact]
public void Test_LocalWatchFailedNotLoggedIn()
{
    // Not logged in -> "Not currently logged into Blink service."
}

[Fact]
public void Test_LocalWatchFailedNoActiveLocalStorage()
{
    // Log in with a fixture that has zero sync modules with Local_Storage_Status == "active"
    // (or construct one with only "Inactive Sync"-equivalent data) -> the "No sync modules with
    // active local storage to watch." message, and no background task started (no way to directly
    // assert "no Task.Run happened" — instead assert Unlocalwatch() immediately afterward returns
    // "Not currently watching local storage.", proving LocalWatch never set _localWatchCts).
}

[Fact]
public void Test_LocalWatchSuccessful_ReturnsStatusMessageNamingActiveSyncModuleCount()
{
    // Login + Map with the NoThumbnailSettings fixture; call LocalWatch; assert the returned
    // message names the correct count of active sync modules (read MockData.cs to get the exact
    // number before asserting it) and ends with "Use UNLOCALWATCH to stop.". Call Unlocalwatch()
    // afterward to clean up.
}

[Fact]
public void Test_LocalWatchFailed_AlreadyWatching()
{
    // Call LocalWatch twice; second call -> "Already watching local storage. Use UNLOCALWATCH to stop."
    // Unlocalwatch() to clean up.
}

[Fact]
public void Test_UnlocalwatchFailedNotWatching()
{
    // Unlocalwatch() with nothing active -> "Not currently watching local storage."
}

[Fact]
public void Test_UnlocalwatchSuccessful_AllowsRestart()
{
    // LocalWatch(); Unlocalwatch() -> "Stopped watching local storage."; LocalWatch() again succeeds.
}

[Fact]
public void Test_Logout_StopsActiveLocalWatchToo()
{
    // Start LocalWatch(), then Logout(); assert Unlocalwatch() afterward returns "Not currently
    // watching local storage." (proving Logout cleared _localWatchCts), mirroring however the
    // existing Logout-stops-Watch test (if one exists — search for it first) proves the same
    // thing for the cloud watcher.
}
```

Give the background task time to actually run at least once where a test needs to observe an alert (e.g. `Thread.Sleep` a short bounded amount after calling `LocalWatch`, then assert on captured alerts) — read how the existing `Watch`-related tests in this file (if any observe alerts rather than just the immediate return string) handle this same timing concern, and match their approach rather than inventing a new one.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "UnitTest_BlinkLibrary"`
Expected: FAIL — `LocalWatch`/`Unlocalwatch` don't exist yet.

- [ ] **Step 3: Implement `LocalWatch`/`Unlocalwatch`/`_localWatchCts`, and update `Logout`**

Exact shape from the spec's "New `LocalWatch`/`Unlocalwatch` pair" section. `Logout` (existing method, already calls `StopWatch()`) gets a parallel `_localWatchCts?.Cancel(); _localWatchCts = null;` (or a small private `StopLocalWatch()` helper mirroring the existing `StopWatch()` — your call which is cleaner given the final shape of both).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "UnitTest_BlinkLibrary"`
Expected: PASS

- [ ] **Step 5: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass, pristine output.

```bash
git add BlinkCameraCall/BlinkLibrary.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Add LocalWatch/Unlocalwatch to BlinkLibrary, stopped by Logout like the cloud watcher"
```

---

### Task 7: `Program.cs` wiring

**Files:**
- Modify: `BlinkCameraCall/Program.cs`

**Interfaces:**
- Consumes: `IBlinkSettings.WatchIntervalSeconds`/`LocalWatchIntervalSeconds` (Task 1), `BlinkLibrary.Watch` with its new trailing parameters (Task 4), `BlinkLibrary.LocalWatch`/`Unlocalwatch` (Task 6).

No test cycle for this task — consistent with this codebase's established precedent that `Program.cs`/`Main`'s console wiring isn't unit-tested (confirmed by Task 7 of the prior plan on this branch's history). Verify by running the app manually against a test account if one is available, or by inspection against the checklist below; run the full automated suite regardless to confirm nothing else broke.

- [ ] **Step 1: Wire `WATCH`'s persisted interval and persistence callback**

`case "watch":` and `TryAutoStartWatch` both currently call `blinkLibrary.Watch(...)` with just the arguments/alertSink (or none, for auto-start). Add `settings.WatchIntervalSeconds` as the third argument and `newSeconds => { settings.WatchIntervalSeconds = (int)newSeconds.TotalSeconds; settings.Save($"{AppContext.BaseDirectory}"); }` as the fourth, at both call sites.

- [ ] **Step 2: Add `case "localwatch"` / `case "unlocalwatch"`**

Same shape as `case "watch"`/`"unwatch"`: `Console.WriteLine(blinkLibrary.LocalWatch(command.Arguments ?? Array.Empty<string>(), alertSink, settings.LocalWatchIntervalSeconds, newSeconds => { settings.LocalWatchIntervalSeconds = (int)newSeconds.TotalSeconds; settings.Save($"{AppContext.BaseDirectory}"); }));` / `Console.WriteLine(blinkLibrary.Unlocalwatch());`. Do **not** add a call to this from `TryAutoStartWatch` or `AttemptAutoLogin` — `LOCALWATCH` stays a manual, explicit command.

- [ ] **Step 3: Add HELP text**

Immediately after the existing `WATCH`/`UNWATCH` HELP lines, add two matching lines for `LOCALWATCH -s<seconds>` and `UNLOCALWATCH`, same column alignment and tone as the surrounding lines.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test`
Expected: all tests pass, pristine output (confirms nothing in `Program.cs`'s neighbors broke; `Program.cs` itself has no tests to run here).

- [ ] **Step 5: Commit**

```bash
git add BlinkCameraCall/Program.cs
git commit -m "Wire LOCALWATCH/UNLOCALWATCH and persisted intervals for both watchers into Program.cs"
```

---

### Task 8: Documentation

**Files:**
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/DEVELOPER.md`

No test cycle (documentation-only). Read the real finished implementation from Tasks 1-7 before writing — in particular the exact alert/retune message wording actually committed in `BlinkLibrary.cs`/`EventWatchPolling.cs`/`LocalStorageWatchPolling.cs`/`Program.cs`'s HELP text — rather than copying the spec's illustrative wording verbatim, in case anything shifted during implementation (mirrors how Task 6 of the prior plan on this branch's history caught and fixed two real inaccuracies this same way).

- [ ] **Step 1: Extend `docs/USER_GUIDE.md`**

Add a new subsection "Watching local storage (`LOCALWATCH`/`UNLOCALWATCH`)" immediately after the existing "Watching for new events (`WATCH`/`UNWATCH`)" subsection, following that subsection's own structure (a short intro, an example transcript, a message table, closing notes). Add one shared paragraph — referenced from both subsections, or placed once right before the first of the two and pointed at from the second — explaining that both watchers' polling interval is self-tuning (grows when a round takes longer than the current interval, never shrinks back) and persisted in `config.json` across runs. Update the existing `WATCH` subsection's own message table with its new retune message, worded exactly as implemented.

- [ ] **Step 2: Extend `docs/DEVELOPER.md`**

Add a new `### 7.11` subsection (immediately after whatever the last `7.x` subsection is at the time this task runs — check the file's current numbering first, don't assume it's still `7.10`) covering: why `AdaptivePollInterval` is a separate, tiny, pure class shared by both watchers rather than duplicated retune math; the `ManifestPolling` cancellation addition and why only the *get* path needs it; `LocalStorageWatchPolling`'s per-sync-module seen-tracking and continue-past-one-module's-error behavior; and why `LOCALWATCH` isn't auto-started on login while `WATCH` is.

- [ ] **Step 3: Commit**

```bash
git add docs/USER_GUIDE.md docs/DEVELOPER.md
git commit -m "Document LOCALWATCH/UNLOCALWATCH and the adaptive, persisted polling intervals"
```
