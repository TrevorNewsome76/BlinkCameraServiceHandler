# Device/Network Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `MAP` console command that prints a readable Network → Sync Module →
Cameras/Doorbells tree with each device's status, built on data `GET -ihome`
already fetches but never displays.

**Architecture:** A new pure formatting extension (`IGetHomeScreenResponse.ToDeviceMapString()`)
turns the existing home-screen response model into a string; a new
`BlinkLibrary.Map()` method fetches the home screen the same way `Get()` already
does and hands the result to the formatter; `Program.cs`/`CommandProcessorExtension`
wire up the `MAP` command name.

**Tech Stack:** C#/.NET 8, xUnit + FluentAssertions, Shadow.Quack duck typing
(`Duck.Implement<T>(...)`).

**Spec:** [docs/superpowers/specs/2026-11-03-device-map-design.md](../specs/2026-11-03-device-map-design.md)

## Global Constraints

- Leave `GET -ihome`'s existing behavior and return value unchanged (spec Goals).
- Do not model `Sirens`/`Chimes`/`Owls`/`Doorbell_Buttons` beyond a bare count
  (spec Non-goals).
- Do not change `IHomeScreenSyncModule.Network_Id`'s type; compare it as a string
  (spec Non-goals / Design).
- Not logged in → exactly `"Not currently logged into Blink service."` (spec
  Error handling, matches existing `VERIFY`/`LOGOUT` wording).
- `GetHomeScreen` returns null → exactly `"No devices found."` (spec Error handling).

## Review Focus

- A network with zero sync modules/cameras/doorbells under it must print
  `"(no cameras or doorbells)"`, not an empty or missing line (spec Error
  handling; a reasonable person expects every network to show *something*).
- A sync module/camera/doorbell whose `Network_Id` matches no network in the
  response must appear under a trailing `"Unassigned devices:"` section, not be
  silently dropped (spec Error handling).
- A camera or doorbell with a null `Signals` must still print (as `wifi n/a`),
  not throw `NullReferenceException` — duck-typed objects return null for
  properties an anonymous object/JSON didn't set, and `GET`'s existing code
  already defends against this pattern with `response?.Networks` etc., so the
  new formatter must do the same for every nested property it reads.
- `IHomeScreenDoorbells` currently has **no `Network_Id` property at all** (confirmed
  by reading the interface directly), so a doorbell can't be grouped under its
  network without adding one — this must be fixed, not routed around, or the
  spec's "Doorbell: ... under its network" design is unimplementable.
- `Sirens`/`Chimes`/`Owls`/`Doorbell_Buttons` being null (not just empty) must
  not throw when checked for "is there at least one" — `MockData`'s existing
  fixture never sets any of the four, so the happy-path test already exercises
  this if the implementation handles null correctly.

---

## Task 1: Fix `IHomeScreenDoorbells.Network_Id` and add `ToDeviceMapString()`

**Files:**
- Modify: `BlinkCommon/Interfaces/System/IHomeScreenDoorbells.cs`
- Modify: `BlinkCameraCallUnitTests/MockData.cs` (`CreateValidHomeScreenDoorbells()`)
- Create: `BlinkCameraCall/Extensions/DeviceMapExtension.cs`
- Create: `BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceMapExtension.cs`

**Interfaces:**
- Produces: `public static string ToDeviceMapString(this IGetHomeScreenResponse response)`
  in namespace `BlinkCameraCall.Extensions` — used by Task 2's `BlinkLibrary.Map()`.
- Produces (interface addition): `long Network_Id { get; }` on `IHomeScreenDoorbells`.

- [ ] **Step 1: Add the missing property and fix the mock fixture**

  In `BlinkCommon/Interfaces/System/IHomeScreenDoorbells.cs`, add `long Network_Id { get; }`
  (same type as `IHomeScreenCamera.Network_Id`).

  In `BlinkCameraCallUnitTests/MockData.cs`, `CreateValidHomeScreenDoorbells()` currently
  sets a property named `Network_id` (lowercase `i`) with value `263178` — this never
  reached the interface because the casing didn't match and the interface had no such
  property. Rename it to `Network_Id = 263178` (matching the interface exactly) so it's
  the one doorbell in the fixture, under network `263178` ("Home Front"), alongside the
  two cameras already there.

- [ ] **Step 2: Run the existing suite to confirm this alone doesn't break anything**

  Run: `dotnet test`
  Expected: PASS (all tests that were passing before, including `UnitTest_System`,
  still pass — both sides of its `BeEquivalentTo` comparison come from the same
  fixture method, so adding a consistently-populated field should not change the
  comparison's outcome).

- [ ] **Step 3: Write the failing tests for `ToDeviceMapString()`**

  Create `BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceMapExtension.cs`:

  ```csharp
  using BlinkCameraCall.Extensions;
  using BlinkCommon.Interfaces.System;
  using FluentAssertions;
  using Shadow.Quack;
  using Xunit;

  namespace BlinkCameraCallUnitTests.Extensions;

  public class UnitTest_DeviceMapExtension
  {
      [Fact]
      public void Test_FullHomeScreen_ProducesGroupedTree()
      {
          var response = MockData.CreateValidHomeScreenResponse();

          var expected =
              "Network: Home Front (disarmed)" + Environment.NewLine +
              "  Sync Module: My Blink Sync Module  [online, fw 16.0.8, wifi 5]" + Environment.NewLine +
              "  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
              "  Camera: Garage  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
              "  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
              Environment.NewLine +
              "Network: Home Back (disarmed)" + Environment.NewLine +
              "  Sync Module: My Blink Sync Module  [offline, fw 4.4.8, wifi 5]" + Environment.NewLine +
              "  (no cameras or doorbells)";

          response.ToDeviceMapString().Should().Be(expected);
      }

      [Fact]
      public void Test_UnassignedDevice_ListedInTrailingSection()
      {
          var response = Duck.Implement<IGetHomeScreenResponse>(new
          {
              Networks = Duck.Implement<List<IHomeScreenNetwork>>(new[]
              {
                  new { Id = 1L, Name = "Only Network", Armed = false }
              }),
              Sync_Modules = Duck.Implement<List<IHomeScreenSyncModule>>(new[]
              {
                  new { Id = 2L, Name = "Orphan Sync", Status = "online", Fw_Version = "1.0", Wifi_Strength = 3, Network_Id = "999" }
              }),
              Cameras = Duck.Implement<List<IHomeScreenCamera>>(Array.Empty<object>()),
              Doorbells = Duck.Implement<List<IHomeScreenDoorbells>>(Array.Empty<object>()),
          });

          var expected =
              "Network: Only Network (disarmed)" + Environment.NewLine +
              "  (no cameras or doorbells)" + Environment.NewLine +
              Environment.NewLine +
              "Unassigned devices:" + Environment.NewLine +
              "  Sync Module: Orphan Sync  [online, fw 1.0, wifi 3]";

          response.ToDeviceMapString().Should().Be(expected);
      }

      [Fact]
      public void Test_NullSignalsAndNoResponse_DoNotThrow()
      {
          var response = Duck.Implement<IGetHomeScreenResponse>(new
          {
              Networks = Duck.Implement<List<IHomeScreenNetwork>>(new[] { new { Id = 1L, Name = "N", Armed = true } }),
              Cameras = Duck.Implement<List<IHomeScreenCamera>>(new[]
              {
                  new { id = 2L, Name = "NoSignalCam", Enabled = true, Status = "done", Battery = "ok", Network_Id = 1L }
              }),
          });

          var expected =
              "Network: N (armed)" + Environment.NewLine +
              "  Camera: NoSignalCam  [enabled, status done, battery ok, wifi n/a]";

          response.ToDeviceMapString().Should().Be(expected);

          Duck.Implement<IGetHomeScreenResponse>(new()).ToDeviceMapString().Should().Be("No devices found.");
      }

      [Fact]
      public void Test_NonEmptyAccessoryArrays_AppendCountLines()
      {
          var response = Duck.Implement<IGetHomeScreenResponse>(new
          {
              Networks = Duck.Implement<List<IHomeScreenNetwork>>(new[] { new { Id = 1L, Name = "N", Armed = false } }),
              Owls = new[] { "owl-1", "owl-2" },
              Sirens = new[] { "siren-1" },
          });

          var expected =
              "Network: N (disarmed)" + Environment.NewLine +
              "  (no cameras or doorbells)" + Environment.NewLine +
              Environment.NewLine +
              "Owls: 2 (not detailed)" + Environment.NewLine +
              "Sirens: 1 (not detailed)";

          response.ToDeviceMapString().Should().Be(expected);
      }
  }
  ```

- [ ] **Step 4: Run the new tests to verify they fail**

  Run: `dotnet test --filter UnitTest_DeviceMapExtension`
  Expected: FAIL (compile error: `ToDeviceMapString` does not exist yet).

- [ ] **Step 5: Implement `ToDeviceMapString()` in `BlinkCameraCall/Extensions/DeviceMapExtension.cs`**

  ```csharp
  namespace BlinkCameraCall.Extensions;

  public static class DeviceMapExtension
  {
      public static string ToDeviceMapString(this IGetHomeScreenResponse response);
  }
  ```

  Treat every one of `response.Networks`, `.Sync_Modules`, `.Cameras`, `.Doorbells`,
  `.Sirens`, `.Chimes`, `.Owls`, `.Doorbell_Buttons` as possibly null (default to
  empty) before use — duck-typed objects return null for anything the caller's
  anonymous object/JSON didn't set, regardless of the interface's declared
  non-nullable type (see Review Focus). If `Networks` ends up empty, return
  `"No devices found."` immediately.

  For each network, in list order: print `Network: {Name} ({armed/disarmed from
  Armed})`, then each sync module/camera/doorbell whose `Network_Id` (sync module:
  `string`, compared via `network.Id.ToString()`; camera/doorbell: `long`, compared
  directly) matches that network's `Id`, each on its own indented line:
  - Sync Module: `  Sync Module: {Name}  [{Status}, fw {Fw_Version}, wifi {Wifi_Strength}]`
  - Camera: `  Camera: {Name}  [{enabled|disabled}, status {Status}, battery {Battery}, wifi {Signals?.Wifi ?? "n/a"}]`
  - Doorbell: same shape as Camera, label `Doorbell:`.

  If a network has none of the three under it, print `  (no cameras or doorbells)`.
  Join each network's block with a blank line between (no trailing blank line).

  After all networks, if any sync module/camera/doorbell's `Network_Id` matched no
  network, append a blank line, `Unassigned devices:`, then each one using the same
  per-device line formats as above.

  After that, for each of `Sirens`/`Chimes`/`Owls`/`Doorbell_Buttons` that is
  non-empty, append a line `"{Name}: {Count} (not detailed)"` (e.g. `Owls: 2 (not
  detailed)`), skipping any that are null or empty.

- [ ] **Step 6: Run the new tests to verify they pass**

  Run: `dotnet test --filter UnitTest_DeviceMapExtension`
  Expected: PASS (4 tests).

- [ ] **Step 7: Run the full suite**

  Run: `dotnet test`
  Expected: PASS (all tests, including the pre-existing ones from Step 2).

- [ ] **Step 8: Commit**

  ```bash
  git add BlinkCommon/Interfaces/System/IHomeScreenDoorbells.cs BlinkCameraCallUnitTests/MockData.cs BlinkCameraCall/Extensions/DeviceMapExtension.cs BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceMapExtension.cs
  git commit -m "Add IHomeScreenDoorbells.Network_Id and device-map formatting"
  ```

---

## Task 2: `BlinkLibrary.Map()`

**Files:**
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`

**Interfaces:**
- Consumes: `IGetHomeScreenResponse.ToDeviceMapString()` (Task 1).
- Consumes (already exists): `IAdapter.GetHomeScreen(IAuthAccount account) : IGetHomeScreenResponse`.
- Produces: `public string Map()` on `BlinkLibrary` — used by Task 3's `Program.cs`.

- [ ] **Step 1: Write the failing tests**

  Add to `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs`:

  ```csharp
  [Fact]
  public void Test_MapFailedNotLoggedIn()
  {
      //assign
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var expected = "Not currently logged into Blink service.";

      //act
      var actualResult = new BlinkLibrary(mockAdapter).Map();

      //assert
      actualResult.Should().BeEquivalentTo(expected);
  }

  [Fact]
  public void Test_MapSuccessful()
  {
      //assign
      var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
      var loginArguments = new[] { "utest@email.com", "pPAssword" };
      var expected =
          "Network: Home Front (disarmed)" + Environment.NewLine +
          "  Sync Module: My Blink Sync Module  [online, fw 16.0.8, wifi 5]" + Environment.NewLine +
          "  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
          "  Camera: Garage  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
          "  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
          Environment.NewLine +
          "Network: Home Back (disarmed)" + Environment.NewLine +
          "  Sync Module: My Blink Sync Module  [offline, fw 4.4.8, wifi 5]" + Environment.NewLine +
          "  (no cameras or doorbells)";

      //act
      var blinkLib = new BlinkLibrary(mockAdapter);
      blinkLib.Login(loginArguments);
      var actualResult = blinkLib.Map();

      //assert
      actualResult.Should().BeEquivalentTo(expected);
  }
  ```

  (This expects the same text as Task 1's `Test_FullHomeScreen_ProducesGroupedTree`
  because `MockHttpClientApiHandler` serves the same `MockData.CreateValidHomeScreenResponse()`
  fixture for the homescreen URL — this test additionally proves the JSON round-trip
  through the mock HTTP layer doesn't change the output.)

- [ ] **Step 2: Run tests to verify they fail**

  Run: `dotnet test --filter UnitTest_BlinkLibrary`
  Expected: FAIL (compile error: `Map` does not exist yet).

- [ ] **Step 3: Implement `Map()` in `BlinkCameraCall/BlinkLibrary.cs`**

  ```csharp
  public string Map()
  ```

  Mirror `Get(string[] arguments)`'s guard clause (`_sessionDetails?.Account is not
  null && _sessionDetails.LoggedInStatus`, else return `"Not currently logged into
  Blink service."` — note `Map()` takes no arguments, so use this wording, not
  `Get`'s different "Not logged in to a system..." wording, per the spec's Error
  handling table). On success, call `adapter.GetHomeScreen(_sessionDetails.Account)`,
  update `_sessionDetails.Networks`/`SyncModules`/`Cameras`/`Doorbells` from the
  response exactly as `Get()` already does, then return `"No devices found."` if
  the response is null, else `response.ToDeviceMapString()`.

- [ ] **Step 4: Run tests to verify they pass**

  Run: `dotnet test --filter UnitTest_BlinkLibrary`
  Expected: PASS.

- [ ] **Step 5: Run the full suite**

  Run: `dotnet test`
  Expected: PASS.

- [ ] **Step 6: Commit**

  ```bash
  git add BlinkCameraCall/BlinkLibrary.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
  git commit -m "Add BlinkLibrary.Map()"
  ```

---

## Task 3: Wire up the `MAP` command

**Files:**
- Modify: `BlinkCameraCall/Program.cs`
- Modify: `BlinkCameraCall/Extensions/CommandProcessorExtension.cs`

**Interfaces:**
- Consumes: `BlinkLibrary.Map()` (Task 2).

- [ ] **Step 1: Add `"map"` to `ValidCommands`**

  In `BlinkCameraCall/Extensions/CommandProcessorExtension.cs`, add `"map"` to the
  `ValidCommands` array (alongside `"login"`, `"logout"`, `"verify"`, `"get"`,
  `"help"`, `"exit"`).

- [ ] **Step 2: Add the `map` case and help line in `Program.cs`**

  In the `switch (command.Command)` block, add:
  ```csharp
  case "map":
      Console.WriteLine(blinkLibrary.Map());
      break;
  ```
  placed next to the existing `"get"` case. In the `"help"` case, add a line after
  the existing `GET` line:
  ```csharp
  Console.WriteLine("MAP                              Shows a tree of networks, sync modules, cameras and doorbells.");
  ```

- [ ] **Step 3: Build and run the full suite**

  Run: `dotnet build` then `dotnet test`
  Expected: both succeed with 0 errors/failures (this task has no new automated
  test of its own — `Program.cs`'s `Main` is not unit-tested anywhere in this
  codebase today, consistent with how `"get"`/`"verify"` are wired up).

- [ ] **Step 4: Manual smoke test**

  Run `dotnet run --project BlinkCameraCall`, log in with real credentials, then
  run `MAP` and confirm it prints a tree instead of erroring, and that `HELP` lists
  it.

- [ ] **Step 5: Commit**

  ```bash
  git add BlinkCameraCall/Program.cs BlinkCameraCall/Extensions/CommandProcessorExtension.cs
  git commit -m "Wire up the MAP command"
  ```
