# Manual Arm/Disarm (system / hub / device) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `ARM`/`DISARM` commands that arm/disarm Blink's network-level "armed" state and each camera/doorbell's `Enabled` flag, at whichever of three levels (whole system, one hub, one device) the user is currently `cd`'d into.

**Architecture:** Six new async-command API methods (reusing the existing `TriggerSnapshot` polling helper), three new `BlinkLibrary.Arm`/`Disarm` overloads (device/hub/system) with hub/system cascading to children, and two new `ShellSession` methods that resolve the current `cd` context and delegate — no new business logic in the shell layer, mirroring `Trigger`'s existing split.

**Tech Stack:** C# / .NET 10, xUnit, FluentAssertions, Shadow.Quack duck typing, the existing `Dependency.Shelf` service-location pattern.

**Spec:** [docs/superpowers/specs/2026-11-09-arm-disarm-design.md](../specs/2026-11-09-arm-disarm-design.md)

## Global Constraints

- Every new adapter/transaction method takes `TimeSpan? pollInterval = null, int maxRetries = 120`, matching `SystemTriggerCameraSnapshot`'s exact signature shape.
- URLs are exactly as the spec's table gives them — no path variations.
- `ARM`/`DISARM` take **zero** arguments anywhere (no flag-style form) — the level is determined entirely by `ShellSession`'s current `cd` context.
- The hub/system cascading loop catches `Exception` broadly per item (not just `InvalidOperationException`) so one failing item never stops the rest.
- A successful `Arm`/`Disarm` at every level refreshes `_sessionDetails.Networks`/`SyncModules`/`Cameras`/`Doorbells` via one new `RefreshDeviceLists()` private helper; the three pre-existing duplicates of that refresh block (`Get`, `Snap`, `Map`) are left untouched.
- Owls, Sirens, Chimes, and Doorbell_Buttons are never targeted by hub/system cascading — they stay unmodeled, same as `MAP` already treats them.
- Device-level `Arm`/`Disarm` resolves the device name via the existing private `ResolveCameraOrDoorbell(name)` (treats a name matching both a camera and a doorbell as not-found) — it does **not** take pre-resolved camera/doorbell objects.

## Review Focus

- A hub with zero child cameras/doorbells (network `Home Back`, id `263310`, in the shared fixture) must still arm/disarm cleanly and report `(1/1 succeeded)`, not divide-by-zero or an empty/garbled line.
- A device name matching both a camera and a doorbell must be treated as not-found (same rule `Snap` already relies on), not silently resolved to one or the other.
- A failure on the **network's own** arm/disarm call (not a child) must still produce a correctly formatted line and the loop must still continue on to every child — the network item is not special-cased out of the "continue and report" behavior.
- Hub/system-level cascading must not throw a `NullReferenceException` when `_sessionDetails.Cameras` and/or `Doorbells` are `null` (networks loaded, but no camera/doorbell arrays at all).
- `ShellSession` keeps its own `_currentNetwork`/`_currentDeviceName` across a `LOGOUT` (it doesn't listen for that event) — calling `ARM`/`DISARM` while still "inside" a hub/device but no longer logged in must still produce `BlinkLibrary`'s own `"Not currently logged into Blink service."`, not a crash from a stale reference.

---

### Task 1: Device-level `Arm`/`Disarm` (one camera or doorbell)

**Files:**
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Consumes: `BlinkLibrary`'s existing private `ResolveCameraOrDoorbell(string name)` (`BlinkLibrary.cs:118`); existing private `TriggerSnapshot(string triggerUrl, long networkId, string tier, TimeSpan? pollInterval, int maxRetries)` in `BlinkApiTransactions.cs`.
- Produces (for later tasks): `IApiTransactions`/`AdapterExtension`: `EnableCamera`/`DisableCamera`/`EnableDoorbell`/`DisableDoorbell(IAuthAccount account, long networkId, long deviceId, TimeSpan? pollInterval = null, int maxRetries = 120) : ICommandStatusResponse?`. `BlinkLibrary.RefreshDeviceLists()` (private, no params, no return) — Tasks 2 and 3 call this too. `BlinkLibrary.Arm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120) : string`, `BlinkLibrary.Disarm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120) : string`.

- [ ] **Step 1: Add fixtures to `MockClientApiHandler.cs`**

  Add to `Post(string Url)`'s switch (alongside the existing camera-thumbnail cases):
  ```csharp
  case "https://rest-007.immedia-semi.com/network/263178/camera/321343/enable":
      return "{\"id\":1010,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/camera/321343/disable":
      return "{\"id\":1011,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/camera/321380/enable":
      return "{\"id\":1012,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/camera/321380/disable":
      return "{\"message\":\"system busy\"}";
  case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/doorbells/5009/enable":
      return "{\"id\":1014,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/doorbells/5009/disable":
      return "{\"id\":1015,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  ```
  Add to `Get(string Url)`'s switch (alongside the existing `/network/263178/command/{id}` cases):
  ```csharp
  case "https://rest-007.immedia-semi.com/network/263178/command/1010":
      return "{\"id\":1010,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/command/1011":
      return "{\"id\":1011,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/command/1012":
      return "{\"id\":1012,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/command/1014":
      return "{\"id\":1014,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/command/1015":
      return "{\"id\":1015,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
  ```
  (`321343` = Pergoda, always succeeds; `321380` = Garage, enable times out and disable is rejected outright — reused in Task 2's mixed-outcome hub test too; `5009` = the doorbell, always succeeds.)

- [ ] **Step 2: Write the failing tests in `UnitTest_BlinkLibrary.cs`**

  ```csharp
  [Fact]
  public void Test_ArmFailedNotLoggedIn()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var actualResult = new BlinkLibrary(mockAdapter).Arm("Pergoda");
      actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
  }

  [Fact]
  public void Test_ArmFailedNoDevicesLoaded()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var actualResult = blinkLib.Arm("Pergoda");
      actualResult.Should().BeEquivalentTo("Run MAP or GET first to load your devices.");
  }

  [Fact]
  public void Test_ArmFailedDeviceNotFound()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var actualResult = blinkLib.Arm("Unknown Camera");
      actualResult.Should().BeEquivalentTo("No single device named 'Unknown Camera' was found. Run MAP to see exact names.");
  }

  [Fact]
  public void Test_ArmFailedCameraAndDoorbellShareName()
  {
      // Reuses the existing ambiguous-name fixture Test_SnapFailedCameraAndDoorbellShareName
      // already relies on, proving device-level Arm treats a name matching both a camera
      // and a doorbell as not-found, the same way Snap already does.
      var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "unothumbnail@email.com", "pPAssword" });
      blinkLib.Map();
      var actualResult = blinkLib.Arm("Shared Name Device");
      actualResult.Should().BeEquivalentTo("No single device named 'Shared Name Device' was found. Run MAP to see exact names.");
  }

  [Fact]
  public void Test_ArmSuccessful_Camera()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();
      var callsBeforeArm = apiMethods.HomeScreenCallCount;

      var actualResult = blinkLib.Arm("Pergoda", TimeSpan.Zero, 1);

      actualResult.Should().BeEquivalentTo("Armed 'Pergoda'.");
      apiMethods.HomeScreenCallCount.Should().Be(callsBeforeArm + 1);
  }

  [Fact]
  public void Test_ArmSuccessful_Doorbell()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();

      var actualResult = blinkLib.Arm("Front door bell", TimeSpan.Zero, 1);

      actualResult.Should().BeEquivalentTo("Armed 'Front door bell'.");
  }

  [Fact]
  public void Test_ArmFailed_Timeout()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();

      var actualResult = blinkLib.Arm("Garage", TimeSpan.Zero, 1);

      actualResult.Should().BeEquivalentTo("Blink did not finish arming 'Garage' in time.");
  }

  [Fact]
  public void Test_DisarmFailedRejectedOutright()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();

      Assert.Throws<InvalidOperationException>(() => blinkLib.Disarm("Garage", TimeSpan.Zero, 1));
  }
  ```

  Give `Arm`/`Disarm` the signature `(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120)` — matching `Snap`'s existing `(string[] arguments, TimeSpan? pollInterval = null, int maxRetries = 120)` poll-parameter placement, just with a plain `string` instead of the flag-array (there's no flag parsing at this level — see Global Constraints).

- [ ] **Step 3: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_BlinkLibrary.Test_Arm|FullyQualifiedName~UnitTest_BlinkLibrary.Test_Disarm"`
  Expected: every new test fails to compile (`Arm`/`Disarm` don't exist yet).

- [ ] **Step 4: Add the four new methods to `IApiTransactions`, `BlinkApiTransactions`, `AdapterExtension`**

  `IApiTransactions.cs` (after the existing `SystemTriggerDoorbellSnapshot` line):
  ```csharp
  ICommandStatusResponse? SystemEnableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
  ICommandStatusResponse? SystemDisableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
  ICommandStatusResponse? SystemEnableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
  ICommandStatusResponse? SystemDisableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
  ```
  `BlinkApiTransactions.cs` (same location) — each a one-line call into the existing private `TriggerSnapshot`, mirroring exactly how `SystemTriggerCameraSnapshot`/`SystemTriggerDoorbellSnapshot` do it:
  ```csharp
  public ICommandStatusResponse? SystemEnableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
      TriggerSnapshot($"https://rest-{account.Tier}.immedia-semi.com/network/{networkId}/camera/{cameraId}/enable", networkId, account.Tier, pollInterval, maxRetries);
  ```
  (and the matching three others, swapping `/enable` for `/disable`, and the doorbell pair using `https://rest-{account.Tier}.immedia-semi.com/api/v1/accounts/{account.Account_Id}/networks/{networkId}/doorbells/{doorbellId}/enable`/`/disable` — the same URL family `SystemTriggerDoorbellSnapshot` already uses.)

  `AdapterExtension.cs` (same location, dropping the `System` prefix, same as every existing entry):
  ```csharp
  public static ICommandStatusResponse? EnableCamera(this IAdapter adapter, IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
      Transactions.SystemEnableCamera(account, networkId, cameraId, pollInterval, maxRetries);
  ```
  (and the matching three others.)

- [ ] **Step 5: Add `RefreshDeviceLists`, `Arm`, `Disarm` to `BlinkLibrary.cs`**

  ```csharp
  private void RefreshDeviceLists()
  {
      var response = adapter.GetHomeScreen(_sessionDetails.Account!);
      _sessionDetails.Networks = response?.Networks;
      _sessionDetails.SyncModules = response?.Sync_Modules;
      _sessionDetails.Cameras = response?.Cameras;
      _sessionDetails.Doorbells = response?.Doorbells;
  }

  public string Arm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120)
  public string Disarm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120)
  ```

  Body shape for both (verb-parametrized): guard `!(_sessionDetails?.LoggedInStatus ?? false)` → not-logged-in message (same as `Snap`); guard `_sessionDetails.Cameras is null && _sessionDetails.Doorbells is null` → `"Run MAP or GET first to load your devices."`; `ResolveCameraOrDoorbell(deviceName)`; both null → `$"No single device named '{deviceName}' was found. Run MAP to see exact names."`; call `adapter.EnableCamera`/`DisableCamera` if `camera is not null`, else `adapter.EnableDoorbell`/`DisableDoorbell`; `null` result → `$"Blink did not finish {arming/disarming} '{deviceName}' in time."`; otherwise `RefreshDeviceLists()` then `$"{Armed/Disarmed} '{deviceName}'."`. An outright-rejected request's `InvalidOperationException` is not caught — let it propagate, matching `Snap`.

- [ ] **Step 6: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_BlinkLibrary.Test_Arm|FullyQualifiedName~UnitTest_BlinkLibrary.Test_Disarm"`
  Expected: all PASS.

- [ ] **Step 7: Run the full suite, then commit**

  Run: `dotnet test` — expect every test (old and new) passing, pristine output.
  ```bash
  git add BlinkCommon/Interfaces/IApiTransactions.cs BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCall/BlinkLibrary.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
  git commit -m "Add device-level Arm/Disarm (camera and doorbell Enabled toggle)"
  ```

---

### Task 2: Network (hub) `Arm`/`Disarm`, with cascading to its devices

**Files:**
- Modify: `BlinkCommon/Interfaces/IApiTransactions.cs`
- Modify: `BlinkCameraCall/BlinkApiTransactions.cs`
- Modify: `BlinkCameraCall/Extensions/AdapterExtension.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Consumes: Task 1's `adapter.EnableCamera`/`DisableCamera`/`EnableDoorbell`/`DisableDoorbell`, `RefreshDeviceLists()`.
- Produces (for Task 3): private `(List<string> Lines, int Succeeded, int Total) ArmOrDisarmNetwork(IHomeScreenNetwork network, bool arm, TimeSpan? pollInterval, int maxRetries)`. Public `BlinkLibrary.Arm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120) : string`, `Disarm(...)` the same shape.

- [ ] **Step 1: Add fixtures to `MockClientApiHandler.cs`**

  `Post`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/263178/arm":
      return "{\"id\":1020,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/disarm":
      return "{\"id\":1021,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263310/arm":
      return "{\"id\":1022,\"network_id\":263310,\"complete\":false,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263310/disarm":
      return "{\"message\":\"system busy\"}";
  ```
  `Get`:
  ```csharp
  case "https://rest-007.immedia-semi.com/network/263178/command/1020":
      return "{\"id\":1020,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263178/command/1021":
      return "{\"id\":1021,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
  case "https://rest-007.immedia-semi.com/network/263310/command/1022":
      return "{\"id\":1022,\"network_id\":263310,\"complete\":true,\"status_code\":908}";
  ```
  (`263178` = "Home Front", has Pergoda/Garage/the doorbell from Task 1's fixtures. `263310` = "Home Back", has zero cameras/doorbells in the shared `CreateValidHomeScreenResponse` fixture — used for the zero-children test, and its `disarm` is rejected outright — no usable id, so no matching `command/...` GET case — used for the network-itself-fails test below.)

- [ ] **Step 2: Write the failing tests in `UnitTest_BlinkLibrary.cs`**

  ```csharp
  [Fact]
  public void Test_ArmHub_MixedOutcome_ContinuesAndReportsEveryItem()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var homeFront = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Front");
      var expected =
          "Hub 'Home Front': armed." + Environment.NewLine +
          "  Camera 'Pergoda': armed." + Environment.NewLine +
          "  Camera 'Garage': Blink did not finish arming 'Garage' in time." + Environment.NewLine +
          "  Doorbell 'Front door bell': armed." + Environment.NewLine +
          "Armed hub 'Home Front' (3/4 succeeded).";

      var actualResult = blinkLib.Arm(homeFront, TimeSpan.Zero, 1);

      actualResult.Should().Be(expected);
  }

  [Fact]
  public void Test_DisarmHub_NetworkItselfRejected_StillCompletesWithoutThrowing()
  {
      // "Home Back" (263310) has zero child devices, so this isolates the network's OWN
      // call failing (as opposed to Test_ArmHub_MixedOutcome's child-level failure above) —
      // its disarm fixture returns no usable id, proving the network item is caught by the
      // same per-item try/catch as every child, not special-cased out of "continue and report".
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var homeBack = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Back");
      var expected =
          "Hub 'Home Back': Blink rejected the request: {\"message\":\"system busy\"}" + Environment.NewLine +
          "Disarmed hub 'Home Back' (0/1 succeeded).";

      var actualResult = blinkLib.Disarm(homeBack, TimeSpan.Zero, 1);

      actualResult.Should().Be(expected);
  }

  [Fact]
  public void Test_ArmHub_NoChildDevices_SucceedsWithOneOfOne()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var homeBack = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Back");
      var expected =
          "Hub 'Home Back': armed." + Environment.NewLine +
          "Armed hub 'Home Back' (1/1 succeeded).";

      var actualResult = blinkLib.Arm(homeBack, TimeSpan.Zero, 1);

      actualResult.Should().Be(expected);
  }

  [Fact]
  public void Test_ArmHub_NullCamerasAndDoorbells_DoesNotThrow()
  {
      // _sessionDetails.Cameras/Doorbells are null until Map()/Get() first populates them
      // (BlinkLibrary's own Initialize() default). Hub-level Arm takes the network object
      // directly rather than reading it from _sessionDetails.Networks, so it can be called
      // with Cameras/Doorbells still at that default null — proving the cascading loop
      // guards both with `?? new List<...>()` rather than assuming Map() already ran.
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var homeFront = Duck.Implement<IHomeScreenNetwork>(new { Id = 263178L, Name = "Home Front", Armed = false });

      var act = () => blinkLib.Arm(homeFront, TimeSpan.Zero, 1);

      act.Should().NotThrow();
  }

  [Fact]
  public void Test_ArmHubFailedNotLoggedIn()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var homeFront = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Front");
      blinkLib.Logout();

      var actualResult = blinkLib.Arm(homeFront, TimeSpan.Zero, 1);

      actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
  }
  ```

- [ ] **Step 3: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_BlinkLibrary.Test_ArmHub|FullyQualifiedName~UnitTest_BlinkLibrary.Test_DisarmHub"`
  Expected: fail to compile (`Arm(IHomeScreenNetwork)` doesn't exist yet).

- [ ] **Step 4: Add `SystemArmNetwork`/`SystemDisarmNetwork` (+ pass-throughs) the same way Task 1 did**

  Same pattern as Task 1 Step 4, URLs `https://rest-{tier}.immedia-semi.com/network/{networkId}/arm` / `/disarm`.

- [ ] **Step 5: Add `ArmOrDisarmNetwork` and the public hub-level `Arm`/`Disarm` to `BlinkLibrary.cs`**

  ```csharp
  private (List<string> Lines, int Succeeded, int Total) ArmOrDisarmNetwork(IHomeScreenNetwork network, bool arm, TimeSpan? pollInterval, int maxRetries)
  ```

  For the network itself, then every camera/doorbell whose `Network_Id == network.Id` (from `_sessionDetails.Cameras ?? new List<IHomeScreenCamera>()` / `.Doorbells ?? new List<IHomeScreenDoorbells>()` — never assume non-null, per Review Focus), in a `try { ... } catch (Exception ex) { ... }` per item:
  - success (non-null) → `"armed."`/`"disarmed."`, count success.
  - `null` (timeout) → `$"Blink did not finish {arming/disarming} '{name}' in time."`.
  - caught exception → `ex.Message`.
  - network's own line has no leading spaces (`"Hub '{network.Name}': {armed/disarmed}."` or its failure variant); each device's line is indented two spaces (`"  Camera '{name}': ..."` / `"  Doorbell '{name}': ..."`).

  ```csharp
  public string Arm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120)
  public string Disarm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120)
  ```

  Guard not-logged-in (same message as every other method); otherwise call `ArmOrDisarmNetwork`, append `$"{Armed/Disarmed} hub '{network.Name}' ({succeeded}/{total} succeeded)."`, call `RefreshDeviceLists()`, join every line with `Environment.NewLine`.

- [ ] **Step 6: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_BlinkLibrary.Test_ArmHub|FullyQualifiedName~UnitTest_BlinkLibrary.Test_DisarmHub"`
  Expected: all PASS. If the exact wording of `Test_DisarmHub_NetworkItselfRejected_StillCompletesWithoutThrowing`'s or `Test_ArmHub_MixedOutcome_ContinuesAndReportsEveryItem`'s expected string doesn't match your exact rejection message format, adjust the test to match the real `ex.Message` your implementation produces — the point being proven (the operation completes and reports every item instead of throwing or stopping early) matters more than the exact string.

- [ ] **Step 7: Run the full suite, then commit**

  Run: `dotnet test` — expect every test passing.
  ```bash
  git add BlinkCommon/Interfaces/IApiTransactions.cs BlinkCameraCall/BlinkApiTransactions.cs BlinkCameraCall/Extensions/AdapterExtension.cs BlinkCameraCallUnitTests/MockClientApiHandler.cs BlinkCameraCall/BlinkLibrary.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
  git commit -m "Add hub-level Arm/Disarm, cascading to every device underneath it"
  ```

---

### Task 3: System-wide `Arm`/`Disarm`

**Files:**
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Consumes: Task 2's private `ArmOrDisarmNetwork`, `RefreshDeviceLists()`.
- Produces (for Task 4): `BlinkLibrary.Arm(TimeSpan? pollInterval = null, int maxRetries = 120) : string`, `Disarm(...)` the same shape.

No new API-layer or fixture changes — this task is pure `BlinkLibrary` orchestration over networks already proven in Task 2.

- [ ] **Step 1: Write the failing tests in `UnitTest_BlinkLibrary.cs`**

  ```csharp
  [Fact]
  public void Test_ArmSystemFailedNotLoggedIn()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var actualResult = new BlinkLibrary(mockAdapter).Arm();
      actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
  }

  [Fact]
  public void Test_ArmSystemFailedNoHubsLoaded()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      var actualResult = blinkLib.Arm();
      actualResult.Should().BeEquivalentTo("Run MAP or GET first to load your devices.");
  }

  [Fact]
  public void Test_ArmSystem_AcrossBothHubs_ReportsGrandTotal()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
      blinkLib.Map();
      var expected =
          "Hub 'Home Front': armed." + Environment.NewLine +
          "  Camera 'Pergoda': armed." + Environment.NewLine +
          "  Camera 'Garage': Blink did not finish arming 'Garage' in time." + Environment.NewLine +
          "  Doorbell 'Front door bell': armed." + Environment.NewLine +
          "Armed hub 'Home Front' (3/4 succeeded)." + Environment.NewLine +
          "Hub 'Home Back': armed." + Environment.NewLine +
          "Armed hub 'Home Back' (1/1 succeeded)." + Environment.NewLine +
          "Armed the system (4/5 succeeded across 2 hubs).";

      var actualResult = blinkLib.Arm(TimeSpan.Zero, 1);

      actualResult.Should().Be(expected);
  }
  ```

- [ ] **Step 2: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_BlinkLibrary.Test_ArmSystem"`
  Expected: fail to compile (`Arm()` with zero args doesn't exist as a distinct overload yet).

- [ ] **Step 3: Add the system-level `Arm`/`Disarm` overloads to `BlinkLibrary.cs`**

  ```csharp
  public string Arm(TimeSpan? pollInterval = null, int maxRetries = 120)
  public string Disarm(TimeSpan? pollInterval = null, int maxRetries = 120)
  ```

  Guard not-logged-in; `_sessionDetails.Networks is null` → `"Run MAP or GET first to load your devices."`; empty `Networks` → `"No hubs to arm."` / `"No hubs to disarm."`; otherwise call `ArmOrDisarmNetwork` once per network, concatenate each network's lines plus its own `"{Armed/Disarmed} hub '{name}' (.../... succeeded)."` line (i.e. reuse exactly the block the hub-level method would have produced for that network), sum every network's `Succeeded`/`Total` across all of them, append `$"{Armed/Disarmed} the system ({succeeded}/{total} succeeded across {network count} hubs)."`, call `RefreshDeviceLists()` once at the end (not per hub).

- [ ] **Step 4: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_BlinkLibrary.Test_ArmSystem"`
  Expected: all PASS.

- [ ] **Step 5: Run the full suite, then commit**

  Run: `dotnet test` — expect every test passing.
  ```bash
  git add BlinkCameraCall/BlinkLibrary.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
  git commit -m "Add system-wide Arm/Disarm across every hub"
  ```

---

### Task 4: Shell/command layer (`ShellSession`, `Program.cs`, `CommandProcessorExtension`)

**Files:**
- Modify: `BlinkCameraCall/ShellSession.cs`
- Modify: `BlinkCameraCall/Extensions/CommandProcessorExtension.cs`
- Modify: `BlinkCameraCall/Program.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_ShellSession.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_CommandProcessorExtension.cs`

**Interfaces:**
- Consumes: `BlinkLibrary.Arm()`/`Arm(IHomeScreenNetwork)`/`Arm(string)` and the `Disarm` equivalents (Tasks 1-3); `ShellSession`'s existing private fields `_currentNetwork`/`_currentDeviceName`.
- Produces: `ShellSession.Arm(TimeSpan? pollInterval = null, int maxRetries = 120) : string`, `ShellSession.Disarm(TimeSpan? pollInterval = null, int maxRetries = 120) : string` (same poll-parameter shape `Trigger`/`List` already expose, so tests can use `TimeSpan.Zero, 1` instead of the real ~1s/120-retry defaults); `"arm"`/`"disarm"` as recognized commands.

- [ ] **Step 1: Write the failing tests in `UnitTest_ShellSession.cs`**

  ```csharp
  [Fact]
  public void Test_Arm_AtRoot_DelegatesToSystemWide()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(LoginArguments);
      blinkLib.Map();
      var shellSession = new ShellSession(blinkLib);

      var actualResult = shellSession.Arm(TimeSpan.Zero, 1);

      actualResult.Should().StartWith("Hub 'Home Front': armed.");
      actualResult.Should().Contain("Armed the system (");
  }

  [Fact]
  public void Test_Arm_InHub_DelegatesToThatHubOnly()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(LoginArguments);
      blinkLib.Map();
      var shellSession = new ShellSession(blinkLib);
      shellSession.ChangeDirectory("Home Back");

      var actualResult = shellSession.Arm(TimeSpan.Zero, 1);

      actualResult.Should().Be(
          "Hub 'Home Back': armed." + Environment.NewLine +
          "Armed hub 'Home Back' (1/1 succeeded)."
      );
  }

  [Fact]
  public void Test_Disarm_InDevice_DelegatesToThatDeviceOnly()
  {
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(LoginArguments);
      blinkLib.Map();
      var shellSession = new ShellSession(blinkLib);
      shellSession.ChangeDirectory("Home Front");
      shellSession.ChangeDirectory("Pergoda");

      var actualResult = shellSession.Disarm(TimeSpan.Zero, 1);

      actualResult.Should().Be("Disarmed 'Pergoda'.");
  }

  [Fact]
  public void Test_Arm_InDevice_AfterLogout_ReportsNotLoggedIn()
  {
      // ShellSession keeps its own cd state across LOGOUT (it doesn't listen for that event) —
      // Arm must still surface BlinkLibrary's own guard, not crash on a stale reference.
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(LoginArguments);
      blinkLib.Map();
      var shellSession = new ShellSession(blinkLib);
      shellSession.ChangeDirectory("Home Front");
      shellSession.ChangeDirectory("Pergoda");
      blinkLib.Logout();

      var actualResult = shellSession.Arm(TimeSpan.Zero, 1);

      actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
  }
  ```

  These rely on Task 2/3's exact wording (`"Hub 'Home Back': armed." + Environment.NewLine + "Armed hub 'Home Back' (1/1 succeeded)."`, etc.) — if Task 2/3 settled on different exact wording, use that wording here instead; the behavior under test (correct delegation by context) is what matters.

- [ ] **Step 2: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_ShellSession.Test_Arm|FullyQualifiedName~UnitTest_ShellSession.Test_Disarm"`
  Expected: fail to compile (`ShellSession.Arm`/`Disarm` don't exist yet).

- [ ] **Step 3: Add `Arm`/`Disarm` to `ShellSession.cs`**

  ```csharp
  public string Arm(TimeSpan? pollInterval = null, int maxRetries = 120) =>
      _currentDeviceName is not null ? library.Arm(_currentDeviceName, pollInterval, maxRetries) :
      _currentNetwork is not null ? library.Arm(_currentNetwork, pollInterval, maxRetries) :
      library.Arm(pollInterval, maxRetries);

  public string Disarm(TimeSpan? pollInterval = null, int maxRetries = 120) =>
      _currentDeviceName is not null ? library.Disarm(_currentDeviceName, pollInterval, maxRetries) :
      _currentNetwork is not null ? library.Disarm(_currentNetwork, pollInterval, maxRetries) :
      library.Disarm(pollInterval, maxRetries);
  ```

  (Matches `Trigger`'s existing pattern of exposing `pollInterval`/`maxRetries` purely for tests — `Program.cs` never passes them, same as every other poll-backed command.)

- [ ] **Step 4: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_ShellSession.Test_Arm|FullyQualifiedName~UnitTest_ShellSession.Test_Disarm"`
  Expected: all PASS.

- [ ] **Step 5: Write the failing tests in `UnitTest_CommandProcessorExtension.cs`**

  ```csharp
  [Fact]
  public void Test_CommandSplitArm_NoArguments_IsValid()
  {
      string consoleCommand = "arm";
      var expectedResult = Duck.Implement<ICommandAndArguments>(new { Command = "arm" });
      var actualResult = consoleCommand.ProcessCommandString();
      actualResult.Should().BeEquivalentTo(expectedResult);
  }

  [Fact]
  public void Test_CommandSplitDisarm_NoArguments_IsValid()
  {
      string consoleCommand = "disarm";
      var expectedResult = Duck.Implement<ICommandAndArguments>(new { Command = "disarm" });
      var actualResult = consoleCommand.ProcessCommandString();
      actualResult.Should().BeEquivalentTo(expectedResult);
  }
  ```

- [ ] **Step 6: Run the new tests to verify they fail**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_CommandProcessorExtension.Test_CommandSplitArm|FullyQualifiedName~UnitTest_CommandProcessorExtension.Test_CommandSplitDisarm"`
  Expected: fail with `Message` set to `"Unknown command."` (not yet in `ValidCommands`).

- [ ] **Step 7: Add `"arm"`, `"disarm"` to `ValidCommands` in `CommandProcessorExtension.cs`**

  Add both strings to the existing `ValidCommands` array (not `BarePositionalCommands` — they take no argument at all).

- [ ] **Step 8: Run the tests to verify they pass**

  Run: `dotnet test BlinkCameraCallUnitTests --filter "FullyQualifiedName~UnitTest_CommandProcessorExtension.Test_CommandSplitArm|FullyQualifiedName~UnitTest_CommandProcessorExtension.Test_CommandSplitDisarm"`
  Expected: all PASS.

- [ ] **Step 9: Wire `Program.cs`**

  Add to the command switch (alongside the existing `"trigger"` case):
  ```csharp
  case "arm":
      Console.WriteLine(shellSession.Arm());
      break;
  case "disarm":
      Console.WriteLine(shellSession.Disarm());
      break;
  ```
  Add to the `HELP` output (alongside the existing `TRIGGER` line):
  ```
  ARM                               Arms the current system/hub/device (cd to choose the level).
  DISARM                            Disarms the current system/hub/device (cd to choose the level).
  ```

- [ ] **Step 10: Run the full suite, then commit**

  Run: `dotnet test` — expect every test passing.
  ```bash
  git add BlinkCameraCall/ShellSession.cs BlinkCameraCall/Extensions/CommandProcessorExtension.cs BlinkCameraCall/Program.cs BlinkCameraCallUnitTests/UnitTest_ShellSession.cs BlinkCameraCallUnitTests/Extensions/UnitTest_CommandProcessorExtension.cs
  git commit -m "Wire ARM/DISARM commands into the shell, dispatched by cd context"
  ```

---

### Task 5: Documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/DEVELOPER.md`

**Interfaces:**
- Consumes: the finished feature from Tasks 1-4 (exact command names, exact message wording as actually implemented — re-read the real output of a couple of the tests above rather than re-deriving wording from memory).

No test cycle — this is documentation, matching how the `WATCH`/`UNWATCH` feature's own docs commit was handled.

- [ ] **Step 1: Update `README.md`'s intro sentence** to mention arming/disarming at system/hub/device level, in the same list style as the existing clauses (login, logout, PIN, home screen, event watching).

- [ ] **Step 2: Update `docs/USER_GUIDE.md`**
  - Add `ARM`/`DISARM` rows to the commands table.
  - Add a new `## Arming and disarming (ARM / DISARM)` section (placed near `## Navigating with CD/LS`, since it's entirely `cd`-context-driven) covering: the three levels and how `cd` selects between them, an example of each level's output (reuse the actual wording Task 2/3's tests proved), the error-message table (not logged in / no hubs loaded / device not found / timeout — pulled from the spec's Error handling summary table), and the note that device-level never touches its hub's own armed state.

- [ ] **Step 3: Update `docs/DEVELOPER.md`**
  - Add a new `### 7.9 The ARM/DISARM commands` subsection (after the existing `### 7.8` section, if one exists by then, or after `### 7.7`), covering: the network-arm-vs-device-enable distinction from the spec's Context section, the six new endpoints and why each URL matches its existing snapshot-trigger sibling's family, the `ArmOrDisarmNetwork` cascading helper and the broad per-item `catch (Exception)`, why `RefreshDeviceLists` was extracted now rather than earlier, and the `ShellSession` dispatch (zero new business logic there, same as `Trigger`).
  - Add a bullet to "Known limitations": the six new endpoints are a best-effort reconstruction like `VIDEOS`/`IMAGE`, not confirmed live; Owls/Sirens/Chimes/Doorbell_Buttons are never armed/disarmed (unmodeled).

- [ ] **Step 4: Re-read all three changed files once** to check the actual command/message wording they describe matches what Tasks 1-4 actually implemented (not what this plan guessed it would be, if anything changed during implementation).

- [ ] **Step 5: Commit**

  ```bash
  git add README.md docs/USER_GUIDE.md docs/DEVELOPER.md
  git commit -m "Document the ARM/DISARM feature (system/hub/device levels)"
  ```
