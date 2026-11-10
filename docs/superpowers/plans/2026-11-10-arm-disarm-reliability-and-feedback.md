# ARM/DISARM Reliability and Live Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the silent deserialization bug that misreports every successful `TriggerSnapshot`-backed command as "rejected" against real Blink responses, add retry/backoff for genuine "system busy" (307) rejections, and give multi-device `ARM`/`DISARM` live per-item output, a "please wait" spinner, and the ability to cancel.

**Architecture:** A new plain-`System.Text.Json`-based parser (`ParseCommandStatus`) replaces the fragile `Shadow.Quack`-based one at the three places that read a command-status response. `TriggerSnapshot` gains a short retry loop for genuine busy rejections plus a `CancellationToken`. `BlinkLibrary`'s `Arm`/`Disarm` (all three levels) gain trailing optional `onProgress`/`CancellationToken` parameters that stream results instead of batching them, with a distinguishable "cancelled" outcome. A new `ProgressSpinner` (built on the existing `IConsoleIO` seam `ShellLineEditor` already uses) owns the "please wait" animation and interleaves it safely with streamed output, driven by `Program.cs` on a background `Task` watched for a cancel keypress.

**Tech Stack:** C# / .NET 10, xUnit, FluentAssertions, Shadow.Quack duck typing (kept for construction, dropped for parsing in the one place it was unreliable), `System.Text.Json`, `Dependency.Shelf`.

**Spec:** [docs/superpowers/specs/2026-11-10-arm-disarm-reliability-and-feedback-design.md](../specs/2026-11-10-arm-disarm-reliability-and-feedback-design.md)

## Global Constraints

- Every new parameter added to an *existing* public signature is **trailing** and optional, so every current positional call site (production and test) keeps compiling and behaving identically.
- `TriggerSnapshot`'s busy-retry is exactly 3 total POST attempts (the initial one plus two retries), waiting ~1s then ~2s, and only when the rejected response's raw JSON contains `"code":307`; any other rejection still fails on the first attempt, exactly as today.
- `CancellationToken` is checked between devices/hubs and before each busy-retry wait — never used to abort an HTTP call already sent.
- `SNAP`'s two existing public methods (`SystemTriggerCameraSnapshot`/`SystemTriggerDoorbellSnapshot`) are not changed at all; they get the busy-retry and parsing fix "for free" through the shared private `TriggerSnapshot` helper.
- `SystemRequestLocalStorageManifest` and `ManifestPolling` (different response interfaces, no evidence they're broken) are out of scope — do not touch them.
- All 234 existing tests must keep passing, unmodified, with pristine output.

## Review Focus

- The busy-retry loop must check cancellation *before* each backoff sleep, not only before the POST — otherwise a cancel during a ~2s wait wouldn't be honored until after that wait finishes.
- `OperationCanceledException` must be caught by its own `catch` arm *before* `ArmOrDisarmNetwork`'s existing generic `catch (Exception ex)` — otherwise cancellation gets silently absorbed as an ordinary per-item failure and the loop wrongly keeps going instead of stopping.
- The fix for the real reported bug must be proven end-to-end through `BlinkLibrary.Arm`/`Disarm` using the actual rich real-world payload shape, not just a unit test of the new parser in isolation — a parser that works alone proves nothing if `TriggerSnapshot` isn't actually calling it.
- `ProgressSpinner.WriteLine` must not resurrect the animation after `Stop()` — if the spinner isn't active, a line written through it should be a plain write, nothing more.
- `Program.cs`'s existing redirected-input/output branch (used by this project's own non-interactive/piped mode) must take the exact same simple path as before — no spinner, no cancellation watching — since there's no interactive key source to watch in that mode.

---

### Task 1: The parsing fix

**Files:**
- Create: `BlinkCommon/Extensions/CommandStatusJsonExtension.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Driver/CommandPolling.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_CommandStatusJsonExtension.cs` (new)
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Produces: `CommandStatusJsonExtension.ParseCommandStatus(this string? json) : ICommandStatusResponse` — consumed by Task 2 (which further modifies `TriggerSnapshot`) and used as-is by `CommandPolling` and `SystemPrepareLocalStorageClip`.

- [ ] **Step 1: Write the failing tests in a new `UnitTest_CommandStatusJsonExtension.cs`**

  ```csharp
  using BlinkCommon.Extensions;
  using FluentAssertions;
  using Xunit;

  namespace BlinkCameraCallUnitTests.Extensions;

  public class UnitTest_CommandStatusJsonExtension
  {
      // Trimmed from the real logged response that exposed this bug — same field
      // count/shape (many extra fields, two always-present value-type fields
      // *absent*), just with placeholder values.
      private const string RealShapedPayload =
          "{\"id\":3796763377,\"created_at\":\"2026-11-10T10:52:36+00:00\",\"updated_at\":\"2026-11-10T10:52:36+00:00\"," +
          "\"deleted_at\":null,\"execute_time\":\"2026-11-10T10:52:36+00:00\",\"command\":\"arm\",\"state_stage\":\"rest\"," +
          "\"stage_rest\":\"2026-11-10T10:52:36+00:00\",\"stage_cs_db\":null,\"stage_cs_sent\":null,\"stage_sm\":null," +
          "\"stage_dev\":null,\"stage_is\":null,\"stage_lv\":null,\"stage_vs\":null,\"state_condition\":\"new\"," +
          "\"sm_ack\":null,\"lfr_ack\":null,\"sequence\":null,\"attempts\":0,\"transaction\":\"kkfBuDvzrR1QfKEN\"," +
          "\"player_transaction\":\"C7OBxH7JOPUCdT2l\",\"server\":null,\"duration\":null,\"by_whom\":\"iPhone 15 Pro - 50.1\"," +
          "\"diagnostic\":false,\"debug\":\"\",\"opts_1\":0,\"target\":null,\"target_id\":null," +
          "\"trace_parent\":\"00-6af2f77448f7d09132dac3131b0d7665-274f7ef1d25543b6-00\",\"sync_module_id\":1447602," +
          "\"ring_setup_id\":null,\"parent_command_id\":null,\"camera_id\":null,\"siren_id\":null,\"firmware_id\":null," +
          "\"network_id\":856592,\"account_id\":239361}";

      [Fact]
      public void Test_ParseCommandStatus_RealShapedPayload_ReadsIdCorrectly()
      {
          var result = RealShapedPayload.ParseCommandStatus();

          result.Id.Should().Be(3796763377L);
          result.Network_Id.Should().Be(856592L);
      }

      [Fact]
      public void Test_ParseCommandStatus_RealShapedPayload_MissingCompleteAndStatusCode_DefaultsRatherThanThrows()
      {
          var result = RealShapedPayload.ParseCommandStatus();

          result.Complete.Should().BeFalse();
          result.Status_Code.Should().Be(0);
      }

      [Fact]
      public void Test_ParseCommandStatus_SimpleExistingShape_StillParsesIdentically()
      {
          var result = "{\"id\":500,\"network_id\":263178,\"complete\":true,\"status_code\":908}".ParseCommandStatus();

          result.Id.Should().Be(500L);
          result.Network_Id.Should().Be(263178L);
          result.Complete.Should().BeTrue();
          result.Status_Code.Should().Be(908);
      }

      [Fact]
      public void Test_ParseCommandStatus_MalformedJson_ReturnsAllDefaultWithoutThrowing()
      {
          var result = "{not json".ParseCommandStatus();

          result.Id.Should().Be(0L);
          result.Complete.Should().BeFalse();
      }

      [Fact]
      public void Test_ParseCommandStatus_NullOrEmpty_ReturnsAllDefault()
      {
          ((string?)null).ParseCommandStatus().Id.Should().Be(0L);
          "".ParseCommandStatus().Id.Should().Be(0L);
      }
  }
  ```

- [ ] **Step 2: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_CommandStatusJsonExtension"`
  Expected: fail to compile (`ParseCommandStatus` doesn't exist yet).

- [ ] **Step 3: Implement `CommandStatusJsonExtension.cs`**

  ```csharp
  using System.Text.Json;
  using BlinkCommon.Interfaces.System;
  using Shadow.Quack;

  namespace BlinkCommon.Extensions;

  public static class CommandStatusJsonExtension
  {
      public static ICommandStatusResponse ParseCommandStatus(this string? json)
      {
          if (string.IsNullOrEmpty(json))
              return Duck.Implement<ICommandStatusResponse>(new { });

          try
          {
              using var doc = JsonDocument.Parse(json);
              var root = doc.RootElement;

              return Duck.Implement<ICommandStatusResponse>(new
              {
                  Id = ReadLong(root, "id"),
                  Network_Id = ReadLong(root, "network_id"),
                  Complete = root.TryGetProperty("complete", out var complete) && complete.ValueKind == JsonValueKind.True,
                  Status_Code = ReadInt(root, "status_code"),
              });
          }
          catch
          {
              return Duck.Implement<ICommandStatusResponse>(new { });
          }
      }

      private static long ReadLong(JsonElement root, string name) =>
          root.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;

      private static int ReadInt(JsonElement root, string name) =>
          root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;
  }
  ```

  (Uses only `System.Text.Json` + `Shadow.Quack.Duck.Implement` against a plain anonymous object — never `Shadow.Quack.Json.JsonProxy`, which is the piece the spike proved unreliable on this payload shape.)

- [ ] **Step 4: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_CommandStatusJsonExtension"`
  Expected: all PASS.

- [ ] **Step 5: Switch the three call sites to use it**

  - `BlinkCameraCall/BlinkApiTransactions.cs`'s `TriggerSnapshot`: change `rawTrigger.Deserialize<ICommandStatusResponse>()` to `rawTrigger.ParseCommandStatus()`.
  - `BlinkCameraCall/Driver/CommandPolling.cs`'s `WaitForCommandComplete`: change `(api.Get(commandStatusUrl) ?? string.Empty).Deserialize<ICommandStatusResponse>()` to `.ParseCommandStatus()`.
  - `BlinkCameraCall/BlinkApiTransactions.cs`'s `SystemPrepareLocalStorageClip`: change `rawPrepare.Deserialize<ICommandStatusResponse>()` to `rawPrepare.ParseCommandStatus()` (it independently duplicates `TriggerSnapshot`'s pattern against the same interface — same bug, same fix).

- [ ] **Step 6: Add a mock fixture reproducing the real bug, and a `BlinkLibrary`-level test proving the fix end-to-end**

  In `BlinkCameraCallUnitTests/MockClientApiHandler.cs`, add to `Post`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/700000/camera/700005/enable":
      return "{\"id\":700006,\"created_at\":\"2026-11-10T10:52:36+00:00\",\"updated_at\":\"2026-11-10T10:52:36+00:00\"," +
          "\"deleted_at\":null,\"execute_time\":\"2026-11-10T10:52:36+00:00\",\"command\":\"arm\",\"state_stage\":\"rest\"," +
          "\"stage_rest\":\"2026-11-10T10:52:36+00:00\",\"stage_cs_db\":null,\"stage_cs_sent\":null,\"stage_sm\":null," +
          "\"stage_dev\":null,\"stage_is\":null,\"stage_lv\":null,\"stage_vs\":null,\"state_condition\":\"new\"," +
          "\"sm_ack\":null,\"lfr_ack\":null,\"sequence\":null,\"attempts\":0,\"transaction\":\"kkfBuDvzrR1QfKEN\"," +
          "\"player_transaction\":\"C7OBxH7JOPUCdT2l\",\"server\":null,\"duration\":null,\"by_whom\":\"iPhone 15 Pro - 50.1\"," +
          "\"diagnostic\":false,\"debug\":\"\",\"opts_1\":0,\"target\":null,\"target_id\":null," +
          "\"trace_parent\":\"00-6af2f77448f7d09132dac3131b0d7665-274f7ef1d25543b6-00\",\"sync_module_id\":1447602," +
          "\"ring_setup_id\":null,\"parent_command_id\":null,\"camera_id\":null,\"siren_id\":null,\"firmware_id\":null," +
          "\"network_id\":700000,\"account_id\":134166}";
  ```
  and to `Get`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/700000/command/700006":
      return "{\"id\":700006,\"network_id\":700000,\"complete\":true,\"status_code\":908}";
  ```

  In `UnitTest_BlinkLibrary.cs`:
  ```csharp
  [Fact]
  public void Test_ArmSuccessful_RealWorldRichResponseShape_NoLongerMisreportedAsRejected()
  {
      // Reproduces the exact bug this whole plan exists to fix: before the fix,
      // Blink's real (much richer than any prior mock) acceptance response made
      // this throw "Blink rejected the request" even though the command had
      // actually been accepted.
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var camera = Duck.Implement<IHomeScreenCamera>(new { id = 700005L, Name = "Real Shape Camera", Network_Id = 700000L });
      blinkLib.CurrentSession!.Cameras = new List<IHomeScreenCamera> { camera };

      var actualResult = blinkLib.Arm("Real Shape Camera", TimeSpan.Zero, 1);

      actualResult.Should().Be("Armed 'Real Shape Camera'.");
  }
  ```

  (Uses the same "assign directly to the live `CurrentSession.Cameras` after `Login()`, no `Map()`" technique already established in this codebase's prior ARM/DISARM branch, so it doesn't touch the shared `MockData.cs` fixtures at all.)

- [ ] **Step 7: Run the new test to verify it fails against the OLD parser, then passes after Step 5/6's changes**

  Temporarily revert Step 5's `TriggerSnapshot` change (or check out the file at the prior commit) and run the Step 6 test alone to confirm it fails with `"Blink rejected the request: ..."` — this is the RED half of the proof that this test actually exercises the real bug. Then restore Step 5's change and re-run:
  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~Test_ArmSuccessful_RealWorldRichResponseShape"`
  Expected: FAILS with the old parser, PASSES with the new one.

- [ ] **Step 8: Run the full suite, then commit**

  Run: `dotnet test` — expect every test (old and new) passing, pristine output.
  ```bash
  git add BlinkCommon/Extensions/CommandStatusJsonExtension.cs BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Driver/CommandPolling.cs BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCallUnitTests/Extensions/UnitTest_CommandStatusJsonExtension.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
  git commit -m "Fix false-rejection bug: parse command-status responses without Shadow.Quack's fragile JsonProxy"
  ```

---

### Task 2: Busy-retry

**Files:**
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Consumes: Task 1's `ParseCommandStatus`.
- Produces: `TriggerSnapshot(..., CancellationToken cancellationToken = default)` (private, unchanged call sites for `SNAP`'s two methods). The six ARM/DISARM methods on `IApiTransactions`/`BlinkApiTransactions`/`AdapterExtension` each gain a new trailing `CancellationToken cancellationToken = default` parameter — consumed by Task 3's `BlinkLibrary` changes.

- [ ] **Step 1: Add fixtures to `MockClientApiHandler.cs`**

  Add a new counter field alongside the existing `HomeScreenCallCount`/`ThrowOnHomeScreen`:
  ```csharp
  public int BusyThenSucceedCallCount { get; private set; }
  public int AlwaysBusyCallCount { get; private set; }
  public int GarageDisableCallCount { get; private set; }
  ```
  Add to `Post`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/700000/camera/700001/enable":
      BusyThenSucceedCallCount++;
      return BusyThenSucceedCallCount >= 3
          ? "{\"id\":700002,\"network_id\":700000,\"complete\":false,\"status_code\":908}"
          : "{\"message\":\"System is busy, please wait\",\"error\":null,\"code\":307}";
  case "https://rest-007.immedia-semi.com/network/700000/camera/700003/enable":
      AlwaysBusyCallCount++;
      return "{\"message\":\"System is busy, please wait\",\"error\":null,\"code\":307}";
  ```
  Add to `Get`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/700000/command/700002":
      return "{\"id\":700002,\"network_id\":700000,\"complete\":true,\"status_code\":908}";
  ```
  In the existing `case "https://rest-007.immedia-semi.com/network/263178/camera/321380/disable":` (Garage — already returns `"{\"message\":\"system busy\"}"` with no `"code"` field, so it's correctly *not* detected as a busy-307 rejection and must keep failing on the first attempt), add the counter without changing the returned value:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/263178/camera/321380/disable":
      GarageDisableCallCount++;
      return "{\"message\":\"system busy\"}";
  ```

- [ ] **Step 2: Write the failing tests in `UnitTest_BlinkLibrary.cs`**

  ```csharp
  [Fact]
  public void Test_ArmFailed_BusyRejection_RetriesAndEventuallySucceeds()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var camera = Duck.Implement<IHomeScreenCamera>(new { id = 700001L, Name = "Busy Then OK", Network_Id = 700000L });
      blinkLib.CurrentSession!.Cameras = new List<IHomeScreenCamera> { camera };
      var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();

      var actualResult = blinkLib.Arm("Busy Then OK", TimeSpan.Zero, 1);

      actualResult.Should().Be("Armed 'Busy Then OK'.");
      apiMethods.BusyThenSucceedCallCount.Should().Be(3);
  }

  [Fact]
  public void Test_ArmFailed_AlwaysBusy_GivesUpAfterExactlyThreeAttempts()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var camera = Duck.Implement<IHomeScreenCamera>(new { id = 700003L, Name = "Always Busy", Network_Id = 700000L });
      blinkLib.CurrentSession!.Cameras = new List<IHomeScreenCamera> { camera };
      var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();

      Assert.Throws<InvalidOperationException>(() => blinkLib.Arm("Always Busy", TimeSpan.Zero, 1));

      apiMethods.AlwaysBusyCallCount.Should().Be(3);
  }

  [Fact]
  public void Test_DisarmFailed_NonBusyRejection_DoesNotRetry()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();

      Assert.Throws<InvalidOperationException>(() => blinkLib.Disarm("Garage", TimeSpan.Zero, 1));

      apiMethods.GarageDisableCallCount.Should().Be(1);
  }
  ```

  Use `TimeSpan.Zero, 1` for the existing `pollInterval`/`maxRetries` — Step 4's `TriggerSnapshot` implementation deliberately reuses `pollInterval` as the busy-retry backoff's base unit too (not a separate parameter), specifically so passing `TimeSpan.Zero` in these tests makes both retry waits instant, exactly like it already does for completion-polling. In production, where `pollInterval` is left `null`, the backoff is the real ~1s/~2s.

  **A deterministic way to test cancelling mid-retry (used by Task 3, set up here):** add a small test-only hook to the mock so a test can cancel a token at the exact instant a POST happens, instead of racing a timer. In `MockHttpClientApiHandler.cs`, add:
  ```csharp
  public Action<string>? OnPostCalled { get; set; }
  ```
  and call `OnPostCalled?.Invoke(Url);` as the very first line of `Post(string Url)` (default `null`, so every existing test is unaffected; takes the URL so a test can target one specific call rather than the first POST made for *anything*, including unrelated setup calls). Add fixtures for this, under the same network as the others: the network's own arm succeeding normally (so the loop reaches the camera below instead of stopping on the network item itself), plus one more always-busy camera as a *child* of that network alongside another sibling — so Task 3's test can prove the loop stops before reaching the sibling, not just that this one device reports cancelled:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/700000/arm":
      return "{\"id\":700008,\"network_id\":700000,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/700000/camera/700007/enable":
      return "{\"message\":\"System is busy, please wait\",\"error\":null,\"code\":307}";
  ```
  and to `Get`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/700000/command/700008":
      return "{\"id\":700008,\"network_id\":700000,\"complete\":true,\"status_code\":908}";
  ```
  (Task 3's test sets `apiMethods.OnPostCalled = url => { if (url.Contains("700007")) cts.Cancel(); };` so only camera `700007`'s call triggers cancellation, not the network's own — then calls `Arm` on a hand-built network with camera `700007` *and* a second sibling camera. The hook fires during `700007`'s first POST, so the very next `cancellationToken.ThrowIfCancellationRequested()` — the one immediately before the backoff sleep — throws, proving both that the retry-wait check actually works and that `ArmOrDisarmNetwork`'s `catch (OperationCanceledException)` arm catches it instead of the generic `catch (Exception ex)`, since the sibling is never attempted.)

- [ ] **Step 3: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~Test_ArmFailed_BusyRejection|FullyQualifiedName~Test_ArmFailed_AlwaysBusy|FullyQualifiedName~Test_DisarmFailed_NonBusyRejection"`
  Expected: fail — currently there's no retry at all, so the busy-then-succeed case fails immediately (`BusyThenSucceedCallCount` would be 1, not 3) and the always-busy case fails after exactly 1 call, not 3.

- [ ] **Step 4: Add `IsBusyRejection` and the retry loop to `TriggerSnapshot`**

  In `BlinkApiTransactions.cs`:
  ```csharp
  private static bool IsBusyRejection(string rawResponse)
  {
      try
      {
          using var doc = System.Text.Json.JsonDocument.Parse(rawResponse);
          return doc.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var value) && value == 307;
      }
      catch
      {
          return false;
      }
  }

  private static ICommandStatusResponse? TriggerSnapshot(string triggerUrl, long networkId, string tier, TimeSpan? pollInterval, int maxRetries, CancellationToken cancellationToken = default)
  {
      if (ApiDriver is null)
          return null;

      // Reuses pollInterval as the backoff's base unit (not a separate parameter) so
      // tests that pass TimeSpan.Zero for pollInterval get instant retries, the same
      // way they already get instant completion-polling.
      var backoffUnit = pollInterval ?? TimeSpan.FromSeconds(1);
      var busyDelays = new[] { backoffUnit, backoffUnit * 2 };
      var rawTrigger = string.Empty;
      var triggerResult = Duck.Implement<ICommandStatusResponse>(new { });

      for (var attempt = 0; attempt <= busyDelays.Length; attempt++)
      {
          cancellationToken.ThrowIfCancellationRequested();

          rawTrigger = ApiDriver.Post(triggerUrl) ?? string.Empty;
          triggerResult = rawTrigger.ParseCommandStatus();

          if (triggerResult.Id != 0)
              break;

          if (!IsBusyRejection(rawTrigger) || attempt == busyDelays.Length)
              throw new InvalidOperationException($"Blink rejected the request: {rawTrigger}");

          cancellationToken.ThrowIfCancellationRequested();
          Thread.Sleep(busyDelays[attempt]);
      }

      var commandUrl = $"https://rest-{tier}.immedia-semi.com/network/{networkId}/command/{triggerResult.Id}";

      return CommandPolling.WaitForCommandComplete(ApiDriver, commandUrl, pollInterval, maxRetries);
  }
  ```

- [ ] **Step 5: Add the trailing `CancellationToken` parameter to the six ARM/DISARM methods**

  `IApiTransactions.cs`, `BlinkApiTransactions.cs`, `AdapterExtension.cs` — `SystemArmNetwork`/`SystemDisarmNetwork`/`SystemEnableCamera`/`SystemDisableCamera`/`SystemEnableDoorbell`/`SystemDisableDoorbell` (and their `AdapterExtension` pass-throughs `ArmNetwork`/`DisarmNetwork`/`EnableCamera`/`DisableCamera`/`EnableDoorbell`/`DisableDoorbell`) each gain `, CancellationToken cancellationToken = default` after `maxRetries`, forwarded into their `TriggerSnapshot` call. `SystemTriggerCameraSnapshot`/`SystemTriggerDoorbellSnapshot` are **not** touched — leave their `TriggerSnapshot` calls exactly as they are (no token argument), which implicitly uses `TriggerSnapshot`'s own `default`.

- [ ] **Step 6: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~Test_ArmFailed_BusyRejection|FullyQualifiedName~Test_ArmFailed_AlwaysBusy|FullyQualifiedName~Test_DisarmFailed_NonBusyRejection"`
  Expected: all PASS (allow ~5-6 real seconds for this filtered run given the backoff sleeps).

- [ ] **Step 7: Run the full suite, then commit**

  Run: `dotnet test` — expect every test passing, pristine output.
  ```bash
  git add BlinkCameraCall/BlinkApiTransactions.cs BlinkCommon/Interfaces/IApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
  git commit -m "Retry genuine 'system busy' (307) rejections up to 3 attempts before giving up"
  ```

---

### Task 3: `BlinkLibrary`/`ShellSession` cancellation and streaming

**Files:**
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCall/ShellSession.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_ShellSession.cs`

**Interfaces:**
- Consumes: Task 2's six `CancellationToken`-accepting adapter methods.
- Produces: `BlinkLibrary.Arm`/`Disarm` at all three levels gain trailing `Action<string>? onProgress = null` (hub/system only — device-level doesn't need it, it only ever produces one line) and `CancellationToken cancellationToken = default`. `ShellSession.Arm`/`Disarm` gain the identical trailing parameters, consumed by Task 5's `Program.cs` wiring.

- [ ] **Step 1: Write the failing tests in `UnitTest_BlinkLibrary.cs`**

  ```csharp
  [Fact]
  public void Test_ArmDeviceFailed_CancelledBeforeCall_ReportsCancelled()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var cts = new CancellationTokenSource();
      cts.Cancel();

      var actualResult = blinkLib.Arm("Pergoda", TimeSpan.Zero, 1, cancellationToken: cts.Token);

      actualResult.Should().Be("Arming 'Pergoda' cancelled.");
  }

  [Fact]
  public void Test_ArmHub_CancelledBeforeCall_ReportsCancelledWithZeroAttempted()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var homeFront = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Front");
      var cts = new CancellationTokenSource();
      cts.Cancel();

      var actualResult = blinkLib.Arm(homeFront, TimeSpan.Zero, 1, cancellationToken: cts.Token);

      actualResult.Should().Be("Arming hub 'Home Front' cancelled (0/0 attempted).");
  }

  [Fact]
  public void Test_ArmHub_StreamsEachLineViaOnProgress()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var homeBack = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Back");
      var streamed = new List<string>();

      var actualResult = blinkLib.Arm(homeBack, TimeSpan.Zero, 1, onProgress: streamed.Add);

      streamed.Should().Equal(
          "Hub 'Home Back': armed.",
          "Armed hub 'Home Back' (1/1 succeeded)."
      );
      actualResult.Should().Be(string.Join(Environment.NewLine, streamed));
  }

  [Fact]
  public void Test_ArmSystem_CancelledDuringFirstHub_StopsBeforeSecondHub()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var cts = new CancellationTokenSource();
      var streamed = new List<string>();
      // Cancel as soon as the first line streams in, before "Home Back" is ever reached.
      void OnProgress(string line) { streamed.Add(line); cts.Cancel(); }

      var actualResult = blinkLib.Arm(TimeSpan.Zero, 1, onProgress: OnProgress, cancellationToken: cts.Token);

      streamed.Should().NotContain(line => line.Contains("Home Back"));
      actualResult.Should().Contain("cancelled");
  }

  [Fact]
  public void Test_ArmHub_CancelledDuringChildsBusyRetry_StopsBeforeSibling()
  {
      // Deterministically exercises Task 2's cancel-before-the-backoff-sleep check AND
      // proves ArmOrDisarmNetwork's catch (OperationCanceledException) arm intercepts it
      // ahead of the generic catch (Exception ex) — if it didn't, the loop would wrongly
      // treat this as an ordinary per-item failure and go on to attempt the sibling below.
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var cts = new CancellationTokenSource();
      var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();
      apiMethods.OnPostCalled = url => { if (url.Contains("700007")) cts.Cancel(); };
      var alwaysBusyCamera = Duck.Implement<IHomeScreenCamera>(new { id = 700007L, Name = "Always Busy Cancel Target", Network_Id = 700000L });
      var siblingCamera = Duck.Implement<IHomeScreenCamera>(new { id = 700009L, Name = "Sibling Never Reached", Network_Id = 700000L });
      blinkLib.CurrentSession!.Cameras = new List<IHomeScreenCamera> { alwaysBusyCamera, siblingCamera };
      var network = Duck.Implement<IHomeScreenNetwork>(new { Id = 700000L, Name = "Retry Cancel Test", Armed = false });

      var actualResult = blinkLib.Arm(network, TimeSpan.Zero, 1, cancellationToken: cts.Token);

      actualResult.Should().NotContain("Sibling Never Reached");
      actualResult.Should().Contain("cancelled");
  }
  ```

  `Test_ArmSystem_CancelledDuringFirstHub_StopsBeforeSecondHub`'s and `Test_ArmHub_CancelledDuringChildsBusyRetry_StopsBeforeSibling`'s exact wording of the cancelled summary lines isn't pinned here — read the real implementation's output once it exists and adjust the assertions to match, keeping the *behavioral* assertions (which device/hub is never reached) as the ones that matter. Note `Test_ArmHub_CancelledDuringChildsBusyRetry_StopsBeforeSibling` relies on `OnPostCalled` firing for *every* `Post` call across the whole test process (it's on the shared mock instance) — since each test constructs its own fresh `MockAdapter`/`MockHttpClientApiHandler` (this codebase's established per-test isolation pattern), this is safe and won't leak into other tests.

- [ ] **Step 2: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~Test_ArmDeviceFailed_Cancelled|FullyQualifiedName~Test_ArmHub_Cancelled|FullyQualifiedName~Test_ArmHub_StreamsEachLine|FullyQualifiedName~Test_ArmSystem_Cancelled"`
  Expected: fail to compile (the new parameters don't exist yet) or fail on assertion (no cancellation/streaming support yet).

- [ ] **Step 3: Thread cancellation and streaming through `BlinkLibrary.cs`**

  Device-level `ArmOrDisarm` gains a trailing `CancellationToken cancellationToken = default`. **Critically, this must actually be passed as the new trailing argument on the `adapter.EnableCamera`/`DisableCamera`/`EnableDoorbell`/`DisableDoorbell` call** (Task 2 added that parameter to all four specifically so callers could do this) — not just stored and left unused, since `TriggerSnapshot`'s own `cancellationToken.ThrowIfCancellationRequested()` checks (between busy-retry attempts) only ever see it if it's threaded all the way down through this call. Wrap that call so an `OperationCanceledException` from it is caught *before* whatever other handling exists, returning `$"{(arm ? "Arming" : "Disarming")} '{deviceName}' cancelled."`.

  `ArmOrDisarmNetwork` gains `Action<string>? onProgress` and `CancellationToken cancellationToken` parameters and a 4th tuple element:
  ```csharp
  private (List<string> Lines, int Succeeded, int Total, bool Cancelled) ArmOrDisarmNetwork(IHomeScreenNetwork network, bool arm, TimeSpan? pollInterval, int maxRetries, Action<string>? onProgress, CancellationToken cancellationToken)
  ```
  Add a local `void Emit(string line) { lines.Add(line); onProgress?.Invoke(line); }` and replace every existing `lines.Add(...)` call in this method with `Emit(...)`. Before the network's own attempt and before each `foreach` iteration (camera, then doorbell), add:
  ```csharp
  if (cancellationToken.IsCancellationRequested)
      return (lines, succeeded, total, true);
  ```
  **Pass `cancellationToken` as the new trailing argument on all three existing adapter calls inside this method** (`adapter.ArmNetwork`/`DisarmNetwork` for the network itself, `adapter.EnableCamera`/`DisableCamera` in the camera loop, `adapter.EnableDoorbell`/`DisableDoorbell` in the doorbell loop) — same reasoning as the device-level change above; without this, the `IsCancellationRequested` check above only ever catches cancellation *between* items, never during one item's own busy-retry wait, which the spec's Review Focus explicitly calls out as required. In each of the three existing `try { ... } catch (Exception ex) { ... }` blocks (network, camera, doorbell), add a `catch (OperationCanceledException) { return (lines, succeeded, total, true); }` arm **before** the existing `catch (Exception ex)`.

  Hub-level `Arm(IHomeScreenNetwork, ...)`/`Disarm(...)` gain the same trailing `onProgress`/`cancellationToken` parameters, forward them to `ArmOrDisarmNetwork`, and branch on the returned `Cancelled`:
  ```csharp
  var closingLine = cancelled
      ? $"{(arm ? "Arming" : "Disarming")} hub '{network.Name}' cancelled ({succeeded}/{total} attempted)."
      : $"{(arm ? "Armed" : "Disarmed")} hub '{network.Name}' ({succeeded}/{total} succeeded).";
  lines.Add(closingLine);
  onProgress?.Invoke(closingLine);
  ```
  (Still run the existing `RefreshDeviceLists()` try/catch afterward regardless of cancellation — a quick final state refresh is harmless and keeps displayed state accurate either way.)

  System-level `ArmOrDisarmSystem` gains the same trailing parameters; its `foreach` over networks checks the returned `Cancelled` after each iteration and `break`s out of the loop when true (after appending that network's own lines/closing line, same shape as above); track whether any network was cancelled, and build the final line as either the existing `"{done} the system (.../... succeeded across N hubs)."` or, if cancelled, something like `"{Arming/Disarming} the system cancelled ({grandSucceeded}/{grandTotal} attempted across {networksReached} of {totalNetworks} hubs)."` — pick exact wording consistent with the hub-level cancelled line's style, and update this plan's Step 1 test assertions to match whatever you land on if it differs from the sketch there.

- [ ] **Step 4: Thread the same parameters through `ShellSession.cs`**

  ```csharp
  public string Arm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default) =>
      _currentDeviceName is not null ? library.Arm(_currentDeviceName, pollInterval, maxRetries, _currentNetwork!.Id, cancellationToken) :
      _currentNetwork is not null ? library.Arm(_currentNetwork, pollInterval, maxRetries, onProgress, cancellationToken) :
      library.Arm(pollInterval, maxRetries, onProgress, cancellationToken);
  ```
  (and the `Disarm` equivalent.) Note device-level `library.Arm(string, ...)` doesn't take `onProgress` at all (per Task 3's own scope above) — only `cancellationToken` is passed through on that branch.

- [ ] **Step 5: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~Test_ArmDeviceFailed_Cancelled|FullyQualifiedName~Test_ArmHub_Cancelled|FullyQualifiedName~Test_ArmHub_StreamsEachLine|FullyQualifiedName~Test_ArmSystem_Cancelled"`
  Expected: all PASS.

- [ ] **Step 6: Add `UnitTest_ShellSession.cs` dispatch tests for the new parameters**

  ```csharp
  [Fact]
  public void Test_Arm_InHub_StreamsViaOnProgress()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(LoginArguments);
      blinkLib.Map();
      var shellSession = new ShellSession(blinkLib);
      shellSession.ChangeDirectory("Home Back");
      var streamed = new List<string>();

      shellSession.Arm(TimeSpan.Zero, 1, onProgress: streamed.Add);

      streamed.Should().NotBeEmpty();
  }

  [Fact]
  public void Test_Arm_AtRoot_CancelledBeforeCall_ReportsCancelled()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(LoginArguments);
      blinkLib.Map();
      var shellSession = new ShellSession(blinkLib);
      var cts = new CancellationTokenSource();
      cts.Cancel();

      var actualResult = shellSession.Arm(TimeSpan.Zero, 1, cancellationToken: cts.Token);

      actualResult.Should().Contain("cancelled");
  }
  ```

- [ ] **Step 7: Run the full suite, then commit**

  Run: `dotnet test` — expect every test passing, pristine output.
  ```bash
  git add BlinkCameraCall/BlinkLibrary.cs BlinkCameraCall/ShellSession.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs BlinkCameraCallUnitTests/UnitTest_ShellSession.cs
  git commit -m "Add cancellation and live streaming to Arm/Disarm at every level"
  ```

---

### Task 4: `ProgressSpinner` and `IConsoleIO.KeyAvailable`

**Files:**
- Modify: `BlinkCameraCall/IO/IConsoleIO.cs`
- Modify: `BlinkCameraCall/IO/SystemConsoleIO.cs`
- Create: `BlinkCameraCall/IO/ProgressSpinner.cs`
- Modify: `BlinkCameraCallUnitTests/IO/FakeConsoleIO.cs`
- Modify: `BlinkCameraCallUnitTests/IO/BlockingFakeConsoleIO.cs`
- Test: `BlinkCameraCallUnitTests/IO/UnitTest_ProgressSpinner.cs` (new)

**Interfaces:**
- Produces: `IConsoleIO.KeyAvailable : bool` (non-blocking). `ProgressSpinner(IConsoleIO io)` with `Start(string)`, `Tick()`, `WriteLine(string)`, `Stop()` — consumed by Task 5's `Program.cs` wiring.

This task is independent of Tasks 1-3 (it only touches the `IO` layer) and can be done in any order relative to them, but is listed after since Task 5 needs both this and Task 3.

- [ ] **Step 1: Write the failing tests in `UnitTest_ProgressSpinner.cs`**

  ```csharp
  using BlinkCameraCall.IO;
  using BlinkCameraCallUnitTests.IO;
  using FluentAssertions;
  using Xunit;

  namespace BlinkCameraCallUnitTests.IO;

  public class UnitTest_ProgressSpinner
  {
      [Fact]
      public void Test_Start_WritesTheMessageOnce()
      {
          var fake = new FakeConsoleIO([]);
          var spinner = new ProgressSpinner(fake);

          spinner.Start("Sending instructions, please wait...");

          fake.WrittenText.Should().Contain(t => t.Contains("Sending instructions, please wait..."));
      }

      [Fact]
      public void Test_Tick_WhileActive_WritesAnotherFrame()
      {
          var fake = new FakeConsoleIO([]);
          var spinner = new ProgressSpinner(fake);
          spinner.Start("Working...");
          var writesAfterStart = fake.WrittenText.Count;

          spinner.Tick();

          fake.WrittenText.Count.Should().BeGreaterThan(writesAfterStart);
      }

      [Fact]
      public void Test_Tick_WhileNotStarted_IsNoOp()
      {
          var fake = new FakeConsoleIO([]);
          var spinner = new ProgressSpinner(fake);

          spinner.Tick();

          fake.WrittenText.Should().BeEmpty();
      }

      [Fact]
      public void Test_WriteLine_WhileActive_PrintsLineAndKeepsAnimating()
      {
          var fake = new FakeConsoleIO([]);
          var spinner = new ProgressSpinner(fake);
          spinner.Start("Working...");

          spinner.WriteLine("Camera 'Pergoda': armed.");

          fake.WrittenText.Should().Contain(t => t.Contains("Camera 'Pergoda': armed."));
          // Spinner should still be active afterward — proven by Tick() producing more output.
          var writesBeforeTick = fake.WrittenText.Count;
          spinner.Tick();
          fake.WrittenText.Count.Should().BeGreaterThan(writesBeforeTick);
      }

      [Fact]
      public void Test_WriteLine_AfterStop_DoesNotResurrectAnimation()
      {
          var fake = new FakeConsoleIO([]);
          var spinner = new ProgressSpinner(fake);
          spinner.Start("Working...");
          spinner.Stop();

          spinner.WriteLine("Done.");
          var writesAfterWriteLine = fake.WrittenText.Count;
          spinner.Tick();

          fake.WrittenText.Count.Should().Be(writesAfterWriteLine, "Tick() after Stop() must still be a no-op even if WriteLine was called in between");
      }

      [Fact]
      public void Test_Stop_ThenTick_IsNoOp()
      {
          var fake = new FakeConsoleIO([]);
          var spinner = new ProgressSpinner(fake);
          spinner.Start("Working...");
          spinner.Stop();
          var writesAfterStop = fake.WrittenText.Count;

          spinner.Tick();

          fake.WrittenText.Count.Should().Be(writesAfterStop);
      }
  }
  ```

- [ ] **Step 2: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_ProgressSpinner"`
  Expected: fail to compile (`ProgressSpinner` doesn't exist yet).

- [ ] **Step 3: Add `KeyAvailable` to `IConsoleIO`, `SystemConsoleIO`, `FakeConsoleIO`, `BlockingFakeConsoleIO`**

  `IConsoleIO.cs`: add `bool KeyAvailable { get; }` alongside the existing `ReadKey()`.
  `SystemConsoleIO.cs`: `public bool KeyAvailable => Console.KeyAvailable;`
  `FakeConsoleIO.cs`: add a settable `public bool KeyAvailable { get; set; }` (defaulting `false`), so tests can simulate "a key is waiting" before calling `ReadKey()`.
  `BlockingFakeConsoleIO.cs`: add `public bool KeyAvailable => /* true when the internal queue has at least one item buffered, without blocking */;` — look at its existing `BlockingCollection<ConsoleKeyInfo>` field for the right non-blocking check (e.g. a count check), purely to keep it compiling against the grown interface; nothing in this plan's tests exercises it.

- [ ] **Step 4: Implement `ProgressSpinner.cs`**

  ```csharp
  namespace BlinkCameraCall.IO;

  public class ProgressSpinner(IConsoleIO io)
  {
      private static readonly char[] Frames = ['|', '/', '-', '\\'];
      private readonly object _lock = new();
      private int _frameIndex;
      private bool _active;

      public void Start(string message)
      {
          lock (_lock)
          {
              io.Write(message + " ");
              _active = true;
              DrawFrame();
          }
      }

      public void Tick()
      {
          lock (_lock)
          {
              if (!_active)
                  return;

              EraseFrame();
              _frameIndex = (_frameIndex + 1) % Frames.Length;
              DrawFrame();
          }
      }

      public void WriteLine(string text)
      {
          lock (_lock)
          {
              if (_active)
                  EraseFrame();

              io.Write(text + Environment.NewLine);

              if (_active)
                  DrawFrame();
          }
      }

      public void Stop()
      {
          lock (_lock)
          {
              if (_active)
                  EraseFrame();

              _active = false;
          }
      }

      private void DrawFrame() => io.Write(Frames[_frameIndex].ToString());

      private void EraseFrame()
      {
          io.CursorLeft = Math.Max(0, io.CursorLeft - 1);
          io.Write(" ");
          io.CursorLeft = Math.Max(0, io.CursorLeft - 1);
      }
  }
  ```

  (One lock shared by all four methods — `Tick()` runs from the console-loop thread while `WriteLine` is invoked from the background operation's thread via `onProgress`, so they must serialize. Unlike `ShellLineEditor`'s redraw math, this only ever moves the cursor back by the one trailing spinner character, so no multi-row wrap handling is needed — `FakeConsoleIO`'s existing `CursorLeft`/`CursorTop` tracking is sufficient for tests.)

- [ ] **Step 5: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_ProgressSpinner"`
  Expected: all PASS.

- [ ] **Step 6: Run the full suite, then commit**

  Run: `dotnet test` — expect every test passing, pristine output.
  ```bash
  git add BlinkCameraCall/IO/IConsoleIO.cs BlinkCameraCall/IO/SystemConsoleIO.cs BlinkCameraCall/IO/ProgressSpinner.cs BlinkCameraCallUnitTests/IO/FakeConsoleIO.cs BlinkCameraCallUnitTests/IO/BlockingFakeConsoleIO.cs BlinkCameraCallUnitTests/IO/UnitTest_ProgressSpinner.cs
  git commit -m "Add ProgressSpinner and non-blocking key-available check for the busy-wait UI"
  ```

---

### Task 5: `Program.cs` wiring

**Files:**
- Modify: `BlinkCameraCall/Program.cs`

**Interfaces:**
- Consumes: Task 3's `ShellSession.Arm(TimeSpan?, int, Action<string>?, CancellationToken)`/`Disarm(...)`; Task 4's `ProgressSpinner`/`IConsoleIO.KeyAvailable`.

No new tests — this project has never had direct `Program.cs` tests (its `Main` is thin orchestration, same reasoning as every prior feature's `Program.cs` changes in this codebase). Verify by building clean and reading the diff carefully against the spec's sketch.

- [ ] **Step 1: Construct one shared `ProgressSpinner` in `Main`**

  Alongside the existing `var shellLineEditor = new ShellLineEditor(new SystemConsoleIO());`, add:
  ```csharp
  var progressSpinner = new ProgressSpinner(new SystemConsoleIO());
  ```

- [ ] **Step 2: Replace the `"arm"`/`"disarm"` cases**

  The enclosing `switch (command.Command.ToLowerInvariant())` already dispatches on a lowercased command, so each `case` block below already statically knows which command it's handling — no need to re-inspect `command.Command` inside either block. Factor the shared busy-loop into one local helper both cases call with the right delegate:

  ```csharp
  case "arm":
      Console.WriteLine(RunWithProgress(progressSpinner, cts => shellSession.Arm(onProgress: progressSpinner.WriteLine, cancellationToken: cts.Token), () => shellSession.Arm()));
      break;
  case "disarm":
      Console.WriteLine(RunWithProgress(progressSpinner, cts => shellSession.Disarm(onProgress: progressSpinner.WriteLine, cancellationToken: cts.Token), () => shellSession.Disarm()));
      break;
  ```

  with a new private static method (placed alongside `Program`'s other small helpers, e.g. near `TryAutoStartWatch`):

  ```csharp
  private static string RunWithProgress(ProgressSpinner progressSpinner, Func<CancellationTokenSource, string> runCancellable, Func<string> runPlain)
  {
      if (Console.IsInputRedirected || Console.IsOutputRedirected)
          return runPlain();

      var cts = new CancellationTokenSource();
      progressSpinner.Start("Sending instructions, please wait...");

      var task = Task.Run(() => runCancellable(cts));

      while (!task.IsCompleted)
      {
          if (Console.KeyAvailable)
          {
              var key = Console.ReadKey(intercept: true);
              if (key.Key == ConsoleKey.Escape || (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                  cts.Cancel();
          }

          progressSpinner.Tick();
          Thread.Sleep(120);
      }

      progressSpinner.Stop();
      return task.Result;
  }
  ```

  (One shared helper avoids duplicating the busy-loop between the `arm` and `disarm` cases; each case just supplies its own two delegates.)

- [ ] **Step 3: Update the `HELP` text for `ARM`/`DISARM`**

  Find the existing `HELP` lines for `ARM`/`DISARM` (added by the prior ARM/DISARM feature) and add a short note that they now show live progress and can be cancelled, e.g. append `" Press Esc to cancel."` to each line, matching the existing terse style of every other `HELP` line.

- [ ] **Step 4: Build and smoke-check**

  Run: `dotnet build` — expect 0 errors (warnings matching the existing baseline are fine).
  Read through the final diff once with fresh eyes: does the redirected-mode branch still take the exact old code path with nothing new added to it? Does the interactive branch correctly default to the real (non-zero, non-`TimeSpan.Zero`) poll parameters in production (i.e. nothing here accidentally hardcodes a test-only fast interval)?

- [ ] **Step 5: Run the full test suite, then commit**

  Run: `dotnet test` — expect every test passing (unrelated to this task, but confirms nothing elsewhere broke).
  ```bash
  git add BlinkCameraCall/Program.cs
  git commit -m "Wire the busy-wait spinner and cancel key into the ARM/DISARM console commands"
  ```

---

### Task 6: Documentation

**Files:**
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/DEVELOPER.md`

**Interfaces:**
- Consumes: the finished behavior from Tasks 1-5 (re-read the real implementation and a couple of the tests above for exact wording rather than re-deriving it from memory).

No test cycle — documentation only, matching this project's established pattern for doc-only commits.

- [ ] **Step 1: Update `docs/USER_GUIDE.md`'s "Arming and disarming" section**

  Add: a note that devices getting a real "system busy" response are retried automatically (up to 3 attempts) before being reported as failed; a description of the "Sending instructions, please wait..." spinner shown for the duration of any `ARM`/`DISARM`; how to cancel (Esc or Ctrl+C) and what happens when you do (whatever's in flight finishes, nothing further is attempted, a summary of what was done prints immediately).

- [ ] **Step 2: Update `docs/DEVELOPER.md`'s §7.9 (or add a new subsection immediately after it)**

  Document: the real root cause (Blink's actual command-acceptance response is far richer than any prior mock assumed, and `Shadow.Quack`'s `JsonProxy` throws on it, silently swallowed into an all-default object that `TriggerSnapshot` misread as a rejection) and why the fix bypasses `JsonProxy` entirely for `ICommandStatusResponse` rather than patching or fully diagnosing the library; the busy-retry's exact shape (3 attempts, ~1s/~2s, keyed off a real `"code":307` field, not message text); why `SystemRequestLocalStorageManifest`/`ManifestPolling` were deliberately left untouched (different interfaces, no evidence of the same bug); the cancellation/streaming design (`onProgress`/`CancellationToken` as trailing optional parameters, the `Cancelled` tuple field, why `OperationCanceledException` needs its own catch arm ahead of the generic one); and `ProgressSpinner`'s role and its one explicitly-accepted limitation (no arbitration with `WATCH`'s alert sink).

- [ ] **Step 3: Re-read both changed files once** against the real final code/test output to confirm accuracy, the same discipline the prior `ARM`/`DISARM` feature's own documentation task used (and where its first attempt needed a correction after review — re-reading before committing catches that class of mistake for free).

- [ ] **Step 4: Commit**

  ```bash
  git add docs/USER_GUIDE.md docs/DEVELOPER.md
  git commit -m "Document the false-rejection fix, busy-retry, and live feedback for ARM/DISARM"
  ```
