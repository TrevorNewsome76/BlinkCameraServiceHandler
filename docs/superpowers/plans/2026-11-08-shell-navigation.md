# Interactive Shell Navigation & Line Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the terminal a PowerShell-style feel: a protected, path-shaped
prompt (`BC>` / `BC\<Hub>>` / `BC\<Hub>\<Device>>`) with a custom line editor
(cursor movement, command history), plus `cd`/`ls` navigation through a
hub→device tree and context-aware `trigger`/`download` commands — all layered
on top of the existing flag-style commands without changing them.

**Architecture:** Three new, independently-testable pieces: `ShellLineEditor`
(raw key-by-key input behind an `IConsoleIO` seam), `ShellSession` (navigation
state wrapping `BlinkLibrary`), and a small `BlinkLibrary` extension surface
(`CurrentSession`, three extracted internal download helpers, and
`ListDeviceMedia`) that both the legacy flag commands and `ShellSession`
share. `CommandProcessorExtension` gains a second, bare-positional parse path
used only by `cd`/`ls`/`trigger` and bare-name `download`; every other command
keeps using the existing `" -"`-splitting path untouched.

**Tech Stack:** C# / .NET 10, Shadow.Quack duck typing, xUnit + FluentAssertions,
the existing synchronous (`.Result`-based) HTTP style — no `async`/`await`.

**Spec:** [2026-11-08-shell-navigation-design.md](../specs/2026-11-08-shell-navigation-design.md)

## Global Constraints

- No `async`/`await` anywhere — matches every existing command.
- Hubs are `Network`s, not `SyncModule`s (networks' directly-attached
  cameras/doorbells all show up under them).
- Legacy flag-style commands, including `DOWNLOAD -i<id>`, keep working
  unchanged from any directory level.
- Bare `download <name>` only resolves when a current device is set, and only
  against that device's **last `ls` output** — never a stale listing from a
  previously-visited device.
- Device-directory `ls` merges: the current thumbnail (if any), cloud events
  for that device (24h window, reusing `VIDEOS`' existing call), and SD-card
  clips for that device's network's sync module (reusing `LOCALCLIPS`'
  existing call) — a missing or inactive sync module silently yields an empty
  local section, never an error.
- Entry names are `yyyy-MM-dd_HH-mm-ss` from each item's `Created_At`
  (`"current"` for the thumbnail); a name collision appends `_<id>` to the
  **second** entry found, in merge order (current, then cloud, then local).
- Prompt: `BC>` at root, `BC\<Hub>>` in a hub, `BC\<Hub>\<Device>>` in a device.
- Line editor: Backspace/Left/Delete can never move the cursor left of the
  prompt's end column; Up/Down recall in-memory-only history (nothing
  persists across runs); a blank `Enter` and a consecutive exact duplicate
  are never appended to history.
- `InternalsVisibleTo("BlinkCameraCallUnitTests")` is added to
  `BlinkCameraCall` so tests can exercise the new `internal` `BlinkLibrary`
  helpers and `DeviceMediaListing` types directly — no new public API surface
  is added just for testability.

## Review Focus

- Backspace pressed repeatedly at the very start of the input line (right
  after the prompt) — must be a true no-op every time, never throw or let the
  cursor index go negative.
- A device's `ls` must never leak another device's cloud/local entries into
  its own listing, even when both devices have events in the same window/on
  the same sync module.
- `download <name>` typed before `ls` has ever run in the current device, or
  after `cd`-ing to a different device without re-running `ls` there — must
  say `"Run LS first."`, never silently reuse a stale listing from a
  different device.
- Two media entries landing on the identical timestamp-derived name (a cloud
  event and a local clip captured the same second) — `ls` must disambiguate
  with a suffix rather than dropping one entry or letting `download` resolve
  the wrong one.
- `cd ..` executed repeatedly past the root — must settle at root and stay
  there, never throw or wrap around to a device.

---

## Task 1: Command parsing — `cd`/`ls`/`trigger` and bare-name `download`

**Files:**
- Modify: `BlinkCommon/Interfaces/ICommandAndArguments.cs`
- Modify: `BlinkCameraCall/Extensions/CommandProcessorExtension.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_CommandProcessorExtension.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ICommandAndArguments.BareDownload { get; }` (`bool`, defaults to
  `false` when omitted from a `Duck.Implement` object — same as every other
  omitted value-type property already relied on elsewhere in this codebase,
  e.g. `BlinkLibrary`'s default `Duck.Implement<ISessionDetails>(new())`).
  `CommandProcessorExtension.ProcessCommandString` now also recognizes `cd`,
  `ls`, `trigger` as bare-positional (their single argument, if any, is
  `Arguments[0]`, the trimmed remainder of the line after the first word —
  never flag-split), and recognizes bare-name `download` (remainder present
  and not starting with `-`) by setting `BareDownload = true` with
  `Arguments[0]` = the trimmed remainder. `Command` keeps the exact casing the
  user typed, matching the existing behavior for every other command (compare
  `Test_CommandSplitSingleCommand`'s `"FooBar"` staying `"FooBar"`) — do not
  lowercase it.

- [ ] **Step 1: Write failing tests for the bare-positional parse path**

```csharp
[Fact] public void Test_CommandSplitBarePositional_Cd_WithMultiWordTarget()
// "cd Front Door" -> Command "cd", Arguments ["Front Door"]
[Fact] public void Test_CommandSplitBarePositional_Cd_NoTarget()
// "cd" -> Command "cd", Arguments [""]
[Fact] public void Test_CommandSplitBarePositional_Ls_IgnoresTrailingText()
// "ls extra" -> Command "ls", Arguments ["extra"]
[Fact] public void Test_CommandSplitBarePositional_Trigger_NoTarget()
// "trigger" -> Command "trigger", Arguments [""]
[Fact] public void Test_CommandSplitDownload_FlagStyle_StillUsesLegacyParsing()
// "download -i123" -> Command "download", Arguments ["i123"], BareDownload false
[Fact] public void Test_CommandSplitDownload_BareName_SetsBareDownloadFlag()
// "download Front Door Clip" -> Command "download", Arguments ["Front Door Clip"], BareDownload true
```
Build each expected result the same way the existing tests in this file do:
`Duck.Implement<ICommandAndArguments>(new { Command = ..., Arguments = ..., BareDownload = ... })`,
compared with `.Should().BeEquivalentTo(...)`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter UnitTest_CommandProcessorExtension`
Expected: FAIL — `cd`/`ls`/`trigger` aren't valid commands yet and
`BareDownload` doesn't exist.

- [ ] **Step 3: Add `BareDownload` to `ICommandAndArguments`**

```csharp
bool BareDownload { get; }
```

- [ ] **Step 4: Implement the bare-positional branch in `CommandProcessorExtension`**

Add `cd`, `ls`, `trigger` to `ValidCommands`. In `ProcessCommandString`
(called `FormatCommand`/`CreateReturnObject` today), before falling through to
the existing `" -"`-splitting path: find the first space to split the verb
from the remainder (no space → remainder is empty); if the verb
case-insensitively matches `cd`/`ls`/`trigger`, build the return object
directly with `Arguments = new[] { remainder.Trim() }` and skip the old path;
if the verb case-insensitively matches `download` and the trimmed remainder
is non-empty and doesn't start with `-`, build it the same way plus
`BareDownload = true`. Otherwise, call the existing `FormatCommand` path
unchanged. Run the result through the existing `ValidateCommand()` either
way, like today.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter UnitTest_CommandProcessorExtension`
Expected: PASS, all facts; no existing fact in this file changes its expected
result.

- [ ] **Step 6: Commit**

```bash
git add BlinkCommon/Interfaces/ICommandAndArguments.cs BlinkCameraCall/Extensions/CommandProcessorExtension.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_CommandProcessorExtension.cs
git commit -m "Add bare-positional parsing for cd/ls/trigger and bare-name download"
```

---

## Task 2: `ShellLineEditor` — protected prompt, cursor movement, history

**Files:**
- Create: `BlinkCameraCall/IO/IConsoleIO.cs`
- Create: `BlinkCameraCall/IO/SystemConsoleIO.cs`
- Create: `BlinkCameraCall/IO/ShellLineEditor.cs`
- Test: `BlinkCameraCallUnitTests/IO/FakeConsoleIO.cs` (test double, not a test
  file itself — no `[Fact]`s)
- Test: `BlinkCameraCallUnitTests/IO/UnitTest_ShellLineEditor.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  ```csharp
  public interface IConsoleIO
  {
      ConsoleKeyInfo ReadKey();
      void Write(string text);
      int CursorLeft { get; set; }
      int CursorTop { get; set; }
      int WindowWidth { get; }
  }
  public class SystemConsoleIO : IConsoleIO // delegates every member straight to System.Console
  public class ShellLineEditor(IConsoleIO io)
  {
      public string ReadLine(string prompt);
  }
  ```
  `ShellLineEditor` never relies on `Write`'s side effect on the cursor —
  after every edit it explicitly recomputes and sets `CursorLeft`/`CursorTop`
  itself (see Step 5's formula), so `SystemConsoleIO.Write` is a bare
  `Console.Write(text)` with no cursor logic of its own.

- [ ] **Step 1: Create `IConsoleIO`, `SystemConsoleIO`, and the `FakeConsoleIO` test double**

`FakeConsoleIO` (test project, `BlinkCameraCallUnitTests/IO/`):
```csharp
public class FakeConsoleIO : IConsoleIO
{
    public FakeConsoleIO(IEnumerable<ConsoleKeyInfo> keys, int windowWidth = 80);
    public List<string> WrittenText { get; }                       // every Write call, in order
    public List<(int Left, int Top)> CursorPositionsSet { get; }   // every time CursorLeft or CursorTop is set
    public int CursorLeft { get; set; }   // setter also appends to CursorPositionsSet
    public int CursorTop { get; set; }    // setter also appends to CursorPositionsSet
    public int WindowWidth { get; }
    public ConsoleKeyInfo ReadKey();      // dequeues the next scripted key; throws InvalidOperationException if the queue is empty (a scripting bug in the test, not the editor)
    public void Write(string text);       // appends to WrittenText
}
```
Add a small helper the tests will use to build keys, e.g. a `static class Keys`
with `Char(char c)`, `Backspace`, `Delete`, `Left`, `Right`, `Home`, `End`,
`Up`, `Down`, `Enter` each returning a `ConsoleKeyInfo` — printable-char keys
carry their char in `KeyChar` with `Key = ConsoleKey.NoName`; control keys
carry `KeyChar = '\0'` with the matching `ConsoleKey`.

No test for this step — it's a test double and a pure delegation wrapper,
nothing to assert yet.

- [ ] **Step 2: Write failing tests for basic typing, Enter, and the protected prompt**

```csharp
[Fact] public void Test_ReadLine_TypingThenEnter_ReturnsTypedString()
// keys: c, d, Enter -> "cd"
[Fact] public void Test_ReadLine_BackspaceAtStart_IsNoOp()
// keys: Backspace, Backspace, Backspace, x, Enter -> "x" (never throws, buffer unaffected by the leading backspaces)
[Fact] public void Test_ReadLine_Enter_EmptyBuffer_ReturnsEmptyAndDoesNotTouchHistory()
// keys: Enter -> ""; then a second ReadLine with keys: Up, Enter -> "" (nothing to recall)
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter UnitTest_ShellLineEditor`
Expected: FAIL — `ShellLineEditor` doesn't exist.

- [ ] **Step 4: Implement the minimal `ReadLine` loop (printable chars, Backspace, Enter) and the cursor-position formula**

Write `prompt` via `io.Write`; record `promptEndLeft = io.CursorLeft`,
`promptEndTop = io.CursorTop` right after. Maintain `var buffer = new StringBuilder()`
and `var cursorIndex = 0`. For absolute positioning, given a buffer offset
`i`: `var col = promptEndLeft + i; row = promptEndTop + col / io.WindowWidth; col %= io.WindowWidth;`
— a `MoveCursorTo(int i)` helper sets `io.CursorLeft`/`io.CursorTop` from
this. Loop on `io.ReadKey()`: printable char (`!char.IsControl(key.KeyChar)`)
inserts at `cursorIndex`, advances it, redraws the tail from the insertion
point (write the remaining buffer from that point, plus one trailing space to
erase any leftover character from a previous longer line, then `MoveCursorTo(cursorIndex)`);
Backspace is a no-op when `cursorIndex == 0`, otherwise removes the char
before `cursorIndex`, decrements it, redraws the tail the same way; Enter
writes a newline and returns `buffer.ToString()` (history comes in Step 7).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter UnitTest_ShellLineEditor`
Expected: PASS, all three facts.

- [ ] **Step 6: Write failing tests for cursor movement (Left/Right/Home/End/Delete)**

```csharp
[Fact] public void Test_ReadLine_LeftArrow_InsertsAtMovedCursor()
// keys: a, b, c, Left, Left, X, Enter -> "aXbc"
[Fact] public void Test_ReadLine_HomeAndEnd_MoveCursorToBufferEnds()
// keys: a, b, c, Home, X, End, Y, Enter -> "XabcY"
[Fact] public void Test_ReadLine_Delete_RemovesCharAtCursor()
// keys: a, b, c, Home, Delete, Enter -> "bc"
[Fact] public void Test_ReadLine_CursorNeverMovesBeforePromptEnd()
// keys: Left, Left, Backspace, Left, a, Left, Left, Left, Backspace, Enter
// assert every (Left, Top) in fake.CursorPositionsSet with Top == promptEndTop has Left >= promptEndLeft
// (capture promptEndLeft/Top from fake.CursorPositionsSet[0], the position set right after the prompt is written)
```

- [ ] **Step 7: Run to verify failure, implement Left/Right/Home/End/Delete, run to verify pass**

Left/Right clamp `cursorIndex` to `[0, buffer.Length]` and just call
`MoveCursorTo` (no redraw needed — the buffer didn't change). Home sets
`cursorIndex = 0`, End sets it to `buffer.Length` (no redraw). Delete is a
no-op when `cursorIndex == buffer.Length`, otherwise removes the char at
`cursorIndex` and redraws the tail from `cursorIndex` (same trailing-space
erase as Backspace).
Run: `dotnet test --filter UnitTest_ShellLineEditor` before and after.
Expected: FAIL then PASS.

- [ ] **Step 8: Write failing tests for Up/Down history**

```csharp
[Fact] public void Test_ReadLine_UpArrow_RecallsPreviousEntry_DiscardingDraft()
// 1st ReadLine: a, b, c, Enter -> "abc" (appended to history)
// 2nd ReadLine: x, y, Up, Enter -> "abc" (draft "xy" discarded when Up pressed)
[Fact] public void Test_ReadLine_DownArrow_PastNewest_ReturnsToEmpty()
// continuing from history ["abc"]: Up, Down, Enter -> ""
[Fact] public void Test_ReadLine_ConsecutiveDuplicateEntry_NotAppendedAgain()
// four ReadLine calls: "a", "a" (dup), "b", "b" (dup) -> history is ["a","b"], not ["a","a","b","b"]
// 5th ReadLine: Up, Up, Enter -> "a" (two Ups from the top lands on the OLDEST distinct entry;
// if the dup had been appended, two Ups would still be sitting on a "b" entry, not "a")
```

- [ ] **Step 9: Run to verify failure**

Run: `dotnet test --filter UnitTest_ShellLineEditor`
Expected: FAIL — no history support yet.

- [ ] **Step 10: Implement history**

`ShellLineEditor` keeps a `List<string> _history` and a nullable history
cursor across `ReadLine` calls (instance state, not per-call). On Enter: if
`buffer` is non-empty and not equal to `_history[^1]` (when `_history` is
non-empty), append it. Up: if there's an older entry than the current history
cursor position (or no cursor yet — start at the newest), replace `buffer`
with it, set `cursorIndex = buffer.Length`, redraw the whole line from index
0. Down: symmetric, stepping newer; stepping past the newest clears `buffer`
to empty. Reset the history cursor to "none" at the start of every `ReadLine`
call.

- [ ] **Step 11: Run to verify pass**

Run: `dotnet test --filter UnitTest_ShellLineEditor`
Expected: PASS, full file.

- [ ] **Step 12: Commit**

```bash
git add BlinkCameraCall/IO/IConsoleIO.cs BlinkCameraCall/IO/SystemConsoleIO.cs BlinkCameraCall/IO/ShellLineEditor.cs \
  BlinkCameraCallUnitTests/IO/FakeConsoleIO.cs BlinkCameraCallUnitTests/IO/UnitTest_ShellLineEditor.cs
git commit -m "Add ShellLineEditor with a protected prompt, cursor movement, and history"
```

---

## Task 3: `BlinkLibrary` internals — `CurrentSession`, shared download helpers, device media listing

**Files:**
- Create: `BlinkCameraCall/AssemblyInfo.cs`
- Create: `BlinkCameraCall/DeviceMediaListing.cs`
- Create: `BlinkCameraCall/Extensions/DeviceMediaListingExtension.cs`
- Modify: `BlinkCameraCall/BlinkLibrary.cs`
- Modify: `BlinkCameraCallUnitTests/MockClientApiHandler.cs`
- Test: `BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceMediaListingExtension.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs` (additions)

**Interfaces:**
- Consumes: nothing new — only the adapter calls `BlinkLibrary` already uses
  (`GetRecentMedia`, `RequestLocalStorageManifest`, `GetLocalStorageManifest`,
  `PrepareLocalStorageClip`, `BuildLocalStorageClipUrl`, `DownloadBytes`), all
  already present on `IAdapter`/`AdapterExtension` — no `IApiTransactions`/
  `BlinkApiTransactions`/`AdapterExtension` changes in this task.
- Produces:
  ```csharp
  public ISessionDetails? CurrentSession { get; }   // BlinkLibrary: _sessionDetails if LoggedInStatus, else null

  internal enum DeviceMediaKind { Current, Cloud, Local }
  internal record DeviceMediaEntry(string Name, DeviceMediaKind Kind, DateTime? Timestamp, ICloudMediaItem? CloudItem, ILocalStorageClip? LocalClip, IHomeScreenSyncModule? SyncModule, long? ManifestId);
  internal record DeviceMediaListing(List<DeviceMediaEntry> Entries);

  internal static string ToListingString(this DeviceMediaListing listing);  // DeviceMediaListingExtension

  // BlinkLibrary, all internal:
  internal string DownloadCurrentThumbnail(string name, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell, string? mediaDirectory = null);
  internal string DownloadCloudItem(ICloudMediaItem item, string? mediaDirectory = null);
  internal string DownloadLocalClip(IHomeScreenSyncModule syncModule, long manifestId, ILocalStorageClip clip, TimeSpan? pollInterval = null, int maxRetries = 120, string? mediaDirectory = null);
  internal DeviceMediaListing ListDeviceMedia(string deviceName, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell, TimeSpan? pollInterval = null, int maxRetries = 120);
  ```
  **Task 4 consumes all five `internal` `BlinkLibrary` members and the three
  `DeviceMediaListing`/`DeviceMediaEntry`/`DeviceMediaKind` types exactly as
  shown above** — if any signature changes while implementing this task,
  update this block before starting Task 4.

- [ ] **Step 1: Add `[assembly: InternalsVisibleTo("BlinkCameraCallUnitTests")]` in `BlinkCameraCall/AssemblyInfo.cs`**

No test — this is a build-visibility change, proven by every `internal`-member
test below actually compiling and running.

- [ ] **Step 2: Extract `DownloadCurrentThumbnail`/`DownloadCloudItem`/`DownloadLocalClip` from `Image`/`Download`/`LocalDownload` (pure refactor)**

Move each method's post-name-resolution body into the matching `internal`
helper above (identical logic, just parameterized on the already-resolved
camera/doorbell/item/clip instead of re-deriving them from `arguments`);
`Image`/`Download`/`LocalDownload` become: guard clauses + name/id
resolution (unchanged) + "no single device found" check (unchanged) + a
single call into the matching helper. No behavior changes.

- [ ] **Step 3: Run the full existing test suite to confirm no regressions**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS — every existing `Image`/`Download`/`LocalDownload` fact
(`Test_ImageSuccessful`, `Test_DownloadSuccessful`, `Test_LocalDownloadSuccessful`,
every failure-path fact for the three) passes unchanged.

- [ ] **Step 4: Write failing tests for `CurrentSession`**

```csharp
[Fact] public void Test_CurrentSession_NotLoggedIn_ReturnsNull()
[Fact] public void Test_CurrentSession_LoggedIn_ReturnsSessionDetails()
```

- [ ] **Step 5: Run to verify failure, implement, run to verify pass**

`CurrentSession => _sessionDetails.LoggedInStatus ? _sessionDetails : null;`
Run: `dotnet test --filter UnitTest_BlinkLibrary` before and after.
Expected: FAIL then PASS.

- [ ] **Step 6: Add new mock HTTP routes for sync module `390959` (the `utest@email.com` account's already-`active`-storage sync module, network `263178`, account `134166`)**

Add to `MockHttpClientApiHandler`:
```csharp
// Post(string Url, string body) switch — unaffected; these go in Post(string Url):
"https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/request" => "{\"id\":610}",
"https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/710/clip/request/11" => "{\"id\":920}",

// Get(string Url):
"https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/request/610" =>
  "{\"manifest_id\":710,\"clips\":[" +
  "{\"id\":11,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-02T08:00:00+00:00\",\"size\":5555}," +
  "{\"id\":12,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-01T12:00:00+00:00\",\"size\":4444}," +
  "{\"id\":13,\"camera_name\":\"Garage\",\"created_at\":\"2026-11-01T09:00:00+00:00\",\"size\":2222}]}",
"https://rest-007.immedia-semi.com/network/263178/command/920" => "{\"id\":920,\"network_id\":263178,\"complete\":true,\"status_code\":908}",

// GetBytes(string Url):
"https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/710/clip/request/11" =>
  global::System.Text.Encoding.UTF8.GetBytes("fake-local-mp4-bytes-2"),
```
Note: clip `12`'s `created_at` deliberately matches the existing Pergoda
cloud item's (`2026-11-01T12:00:00+00:00`, id `3001`) — this is the
collision fixture Step 7's test needs. Clip `13` (`camera_name: "Garage"`)
exists to prove filtering excludes other devices' clips.

No test for this step by itself — it's exercised by Step 7.

- [ ] **Step 7: Write failing tests for `ListDeviceMedia`**

```csharp
[Fact] public void Test_ListDeviceMedia_MergesCurrentCloudAndLocal_DeduplicatesCollidingNames()
// login utest@email.com, Map(), then:
// var camera = MockData.CreateValidHomeScreenCameras()[0]; // Pergoda
// var result = blinkLib.ListDeviceMedia("Pergoda", camera, null, TimeSpan.Zero, 3);
// result.Entries should have exactly 4 entries:
//   ("current", Current), ("2026-11-01_12-00-00", Cloud), ("2026-11-02_08-00-00", Local), ("2026-11-01_12-00-00_12", Local)
// result.Entries should NOT contain any entry whose LocalClip.Camera_Name == "Garage" (clip 13 excluded)

[Fact] public void Test_ListDeviceMedia_NoThumbnail_NoCloudMatch_NoSyncModuleForNetwork_ReturnsEmptyEntries()
// fabricate: Duck.Implement<IHomeScreenCamera>(new { id = 99999L, Name = "Orphan Cam", Network_Id = 555555L, Thumbnail = "" })
// (Network_Id matches neither real sync module's network; Thumbnail empty; name matches no cloud item)
// result.Entries.Should().BeEmpty()

[Fact] public void Test_ListDeviceMedia_SyncModuleInactive_SkipsLocalSection_ButKeepsCurrent()
// fabricate: Duck.Implement<IHomeScreenCamera>(new { id = 99998L, Name = "Back Cam", Network_Id = 263310L, Thumbnail = "/some/thumb.jpg" })
// (263310 = "Home Back" network, whose real sync module 391167 has Local_Storage_Status "unavailable")
// result.Entries.Should().ContainSingle(e => e.Name == "current" && e.Kind == DeviceMediaKind.Current)
```
`Test_ListDeviceMedia_SyncModuleInactive_SkipsLocalSection_ButKeepsCurrent`
only needs a non-empty `Thumbnail` string, not a real downloadable URL — this
test only checks the listing, never downloads it.

The local-section **timeout** path (manifest poll exhausts `maxRetries`) is
intentionally not re-tested here: `ListDeviceMedia` calls the exact same
`adapter.GetLocalStorageManifest` already covered by `UnitTest_ManifestPolling`
and `Test_LocalClipsTimedOut`, and its handling of a `null` result is a
one-line "skip this section" check with nothing new to get wrong. Building a
dedicated timeout fixture here would mean adding a whole new sync
module/network/camera to `MockData` risking today's `Test_MapSuccessful`
string — not worth it for this one line. (Ledgered here, not silently
skipped.)

- [ ] **Step 8: Run to verify failure**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: FAIL — `ListDeviceMedia` doesn't exist.

- [ ] **Step 9: Implement `DeviceMediaListing`/`DeviceMediaEntry`/`DeviceMediaKind` and `BlinkLibrary.ListDeviceMedia`**

`ListDeviceMedia`: 1) if `camera?.Thumbnail ?? doorbell?.Thumbnail` is
non-empty, start the entry list with a `Current` entry (`Name = "current"`,
`Timestamp = null`). 2) `adapter.GetRecentMedia(_sessionDetails.Account!, DateTime.UtcNow.AddHours(-24))?.Media ?? new()`,
filtered to `!Deleted && string.Equals(Device_Name, deviceName, StringComparison.OrdinalIgnoreCase)`,
each becoming a `Cloud` entry named from `Created_At.ToString("yyyy-MM-dd_HH-mm-ss")`.
3) Find `_sessionDetails.SyncModules?.FirstOrDefault(s => s.Network_Id == (camera?.Network_Id ?? doorbell!.Network_Id).ToString())`;
if found and `Local_Storage_Status == "active"`, request+poll its manifest
(same two adapter calls `LocalClips` already makes) and, if the poll didn't
time out, filter `Clips` to `string.Equals(Camera_Name, deviceName, StringComparison.OrdinalIgnoreCase)`,
each becoming a `Local` entry the same way. Any other case (no matching sync
module, inactive, or a timed-out poll) adds nothing — no error. 4) Walk the
built list with a `HashSet<string>` of seen names; on a collision, rename
that entry to `$"{name}_{(entry.CloudItem?.Id ?? entry.LocalClip!.Id)}"`.
Return `new DeviceMediaListing(entries)`.

- [ ] **Step 10: Run to verify pass**

Run: `dotnet test --filter UnitTest_BlinkLibrary`
Expected: PASS; full suite green.

- [ ] **Step 11: Write failing tests for `DeviceMediaListingExtension.ToListingString`**

```csharp
[Fact] public void Test_ToListingString_SortsCurrentFirstThenNewestDescending()
// hand-built DeviceMediaListing with a Local (2026-11-02 08:00), a Cloud (2026-11-01 12:00), and a Current entry, in that construction order
// expected: "current  [current]" / "2026-11-02_08-00-00  [local]" / "2026-11-01_12-00-00  [cloud]" (newline-joined)
[Fact] public void Test_ToListingString_TiesKeepMergeOrder()
// two entries at the identical Timestamp, Cloud listed before Local in the input list
// expected output keeps Cloud's line before Local's line
[Fact] public void Test_ToListingString_EmptyListing_ReturnsNoMediaMessage()
// new DeviceMediaListing(new()) -> "No media found for this device."
```

- [ ] **Step 12: Run to verify failure**

Run: `dotnet test --filter UnitTest_DeviceMediaListingExtension`
Expected: FAIL — method doesn't exist.

- [ ] **Step 13: Implement `ToListingString`**

Each line is `$"{entry.Name}  [{entry.Kind.ToString().ToLowerInvariant()}]"`.
Order: entries with `Kind == Current` first (there's at most one), then the
rest via `.OrderByDescending(e => e.Timestamp)` (LINQ's `OrderByDescending` is
stable, so equal timestamps keep their original relative order — which is
merge order, cloud before local, per `ListDeviceMedia`'s build order). Empty
input → `"No media found for this device."`.

- [ ] **Step 14: Run to verify pass**

Run: `dotnet test --filter UnitTest_DeviceMediaListingExtension`
Expected: PASS; full suite green.

- [ ] **Step 15: Commit**

```bash
git add BlinkCameraCall/AssemblyInfo.cs BlinkCameraCall/DeviceMediaListing.cs \
  BlinkCameraCall/Extensions/DeviceMediaListingExtension.cs BlinkCameraCall/BlinkLibrary.cs \
  BlinkCameraCallUnitTests/MockClientApiHandler.cs \
  BlinkCameraCallUnitTests/Extensions/UnitTest_DeviceMediaListingExtension.cs BlinkCameraCallUnitTests/UnitTest_BlinkLibrary.cs
git commit -m "Add BlinkLibrary internals for shell navigation: CurrentSession, shared download helpers, device media listing"
```

---

## Task 4: `ShellSession` — tree navigation, device `ls`, `trigger`, `download`-by-name

**Files:**
- Create: `BlinkCameraCall/ShellSession.cs`
- Modify: `BlinkCameraCall/Extensions/DeviceMapExtension.cs`
- Test: `BlinkCameraCallUnitTests/UnitTest_ShellSession.cs`

**Interfaces:**
- Consumes (Task 3): `BlinkLibrary.CurrentSession`, `.Snap(string[], TimeSpan?, int)`
  (existing), `.ListDeviceMedia(...)`, `.DownloadCurrentThumbnail(...)`,
  `.DownloadCloudItem(...)`, `.DownloadLocalClip(...)`, `DeviceMediaListing`/
  `DeviceMediaEntry`/`DeviceMediaKind`, `DeviceMediaListingExtension.ToListingString`.
- Produces:
  ```csharp
  public class ShellSession(BlinkLibrary library)
  {
      public string Prompt { get; }  // "BC>", "BC\\<hub>>", or "BC\\<hub>\\<device>>"
      public string ChangeDirectory(string target);
      public string List(TimeSpan? pollInterval = null, int maxRetries = 120);
      public string Trigger(TimeSpan? pollInterval = null, int maxRetries = 120);
      public string DownloadByName(string name, TimeSpan? pollInterval = null, int maxRetries = 120, string? mediaDirectory = null);
  }
  ```
  **Task 5 consumes exactly this public surface.**

- [ ] **Step 1: Make `DeviceMapExtension`'s per-line formatters reusable and add a network-line formatter**

Change `FormatCameraLine` and `FormatDoorbellLine` from `private` to
`internal`. Add `internal static string FormatNetworkLine(this IHomeScreenNetwork network) => $"Network: {network.Name} ({(network.Armed ? "armed" : "disarmed")})";`
and change `ToDeviceMapString`'s `lines.Add($"Network: {network.Name} ({(network.Armed ? "armed" : "disarmed")})")`
to `lines.Add(network.FormatNetworkLine())` — same output, one less
duplicated format string.

- [ ] **Step 2: Run the existing `DeviceMapExtension`/`Map` tests to confirm no regression**

Run: `dotnet test --filter UnitTest_DeviceMapExtension` and
`dotnet test --filter Test_MapSuccessful`
Expected: PASS, unchanged output.

- [ ] **Step 3: Write failing tests for `Prompt` and `ChangeDirectory`**

```csharp
[Fact] public void Test_Prompt_StartsAtRoot()                               // "BC>"
[Fact] public void Test_ChangeDirectory_NotLoggedIn()                       // "Not currently logged into Blink service."
[Fact] public void Test_ChangeDirectory_ToHub_NoMapRun()                    // logged in only -> "Run MAP or GET first to load your devices."
[Fact] public void Test_ChangeDirectory_ToHub_Successful()                  // "Now in hub 'Home Front'.", Prompt == "BC\\Home Front>"
[Fact] public void Test_ChangeDirectory_ToHub_UnknownName()                 // "No hub named 'Nope' was found. Run LS to see exact names."
[Fact] public void Test_ChangeDirectory_ToDevice_Successful()               // from the hub, "Now in device 'Pergoda'.", Prompt == "BC\\Home Front\\Pergoda>"
[Fact] public void Test_ChangeDirectory_ToDevice_UnknownName()              // "No device named 'Nope' was found. Run LS to see exact names."
[Fact] public void Test_ChangeDirectory_DotDot_FromDeviceToHub()            // "Now in hub 'Home Front'."
[Fact] public void Test_ChangeDirectory_DotDot_FromHubToRoot()              // "Back to root."
[Fact] public void Test_ChangeDirectory_DotDot_AtRoot_IsNoOp()              // "Already at root.", Prompt stays "BC>"
[Fact] public void Test_ChangeDirectory_Backslash_FromDevice_JumpsToRoot()  // "Back to root."
[Fact] public void Test_ChangeDirectory_BareNoTarget_JumpsToRoot()          // ChangeDirectory("") -> "Back to root."
[Fact] public void Test_ChangeDirectory_FromDevice_AnyTarget_Fails()        // from Pergoda, cd "Garage" -> "No device named 'Garage' was found. Run LS to see exact names."
```

- [ ] **Step 4: Run to verify failure**

Run: `dotnet test --filter UnitTest_ShellSession`
Expected: FAIL — `ShellSession` doesn't exist.

- [ ] **Step 5: Implement `Prompt` and `ChangeDirectory`**

State: `IHomeScreenNetwork? _currentNetwork`, `IHomeScreenCamera? _currentCamera`,
`IHomeScreenDoorbells? _currentDoorbell`, `string? _currentDeviceName`.
`Prompt` is computed from these (root: `"BC>"`; hub: `$"BC\\{_currentNetwork.Name}>"`;
device: `$"BC\\{_currentNetwork.Name}\\{_currentDeviceName}>"`).
`ChangeDirectory(target)`: not-logged-in check first; then if the trimmed
target is empty, `"\\"`, or `"/"` → clear all state, return `"Back to root."`;
then if it's `".."` → from device, clear device state and return
`$"Now in hub '{_currentNetwork!.Name}'."`; from hub, clear network state and
return `"Back to root."`; from root, return `"Already at root."` without
changing anything; otherwise, require `library.CurrentSession!.Networks is not null`
(else `"Run MAP or GET first to load your devices."`), and resolve the
(non-trimmed-away) target: at root against `Networks` by name
(case-insensitive); at hub against that network's cameras/doorbells by name
(reuse `FindCameraByName`/`FindDoorbellByName`-style ambiguity-as-null
matching — a direct `FirstOrDefault`/count check is fine here, this doesn't
need to share code with `DeviceLookupExtension` since it's scoped to one
network's list, not the whole session); at device, always fail with the
device-level "not found" message (no lookup attempted, per spec). Successful
hub match → set `_currentNetwork`, return `$"Now in hub '{target}'."`
(use the matched network's actual `Name`, not the raw typed `target,` so
casing in the confirmation matches the real name). Successful device match →
set `_currentCamera`/`_currentDoorbell`/`_currentDeviceName`, return
`$"Now in device '{_currentDeviceName}'."`.

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test --filter UnitTest_ShellSession`
Expected: PASS, all `ChangeDirectory`/`Prompt` facts.

- [ ] **Step 7: Write failing tests for `List` at root and hub level**

```csharp
[Fact] public void Test_List_NotLoggedIn()                  // "Not currently logged into Blink service."
[Fact] public void Test_List_AtRoot_NoMapRun()               // "Run MAP or GET first to load your devices."
[Fact] public void Test_List_AtRoot_ListsHubs()
// "Network: Home Front (disarmed)" + NewLine + "Network: Home Back (disarmed)"
[Fact] public void Test_List_AtHub_ListsDevices()
// after cd "Home Front": "  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]" + NewLine +
// "  Camera: Garage  [enabled, status done, battery ok, wifi 5]" + NewLine +
// "  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]"
// (exact strings copied from Test_MapSuccessful's expected output)
```

- [ ] **Step 8: Run to verify failure, implement, run to verify pass**

`List()`: not-logged-in check; if `_currentDeviceName is not null`, this is
Step 11's job (stub: not reached yet by these tests); else if `Networks is null`
→ the guard message; else if `_currentNetwork is not null`, join
`FormatCameraLine`/`FormatDoorbellLine` over that network's cameras/doorbells
(by `Network_Id`) with `Environment.NewLine`, or a "no devices" message if
both are empty; else join `FormatNetworkLine()` over all `Networks`.
Run: `dotnet test --filter UnitTest_ShellSession` before and after.
Expected: FAIL then PASS.

- [ ] **Step 9: Write failing tests for `Trigger`**

```csharp
[Fact] public void Test_Trigger_NoCurrentDevice()   // "Not in a device directory. cd into a camera or doorbell first."
[Fact] public void Test_Trigger_Successful()
// cd into "Home Front" then "Pergoda", Trigger(TimeSpan.Zero, 1) ->
// "New image captured for 'Pergoda'. Run IMAGE -nPergoda to download it." (reuses the Snap fixture Test_SnapSuccessful_Camera already proved out)
```

- [ ] **Step 10: Run to verify failure, implement, run to verify pass**

`Trigger(pollInterval, maxRetries)`: if `_currentDeviceName is null` → the
"not in a device directory" message; else `library.Snap(new[] { "n" + _currentDeviceName }, pollInterval, maxRetries)`.
Run: `dotnet test --filter UnitTest_ShellSession` before and after.
Expected: FAIL then PASS.

- [ ] **Step 11: Write failing tests for device-level `List` and `DownloadByName`**

```csharp
[Fact] public void Test_List_AtDevice_MergesCurrentCloudAndLocal()
// cd "Home Front", cd "Pergoda", List(pollInterval: TimeSpan.Zero, maxRetries: 3) ->
// "current  [current]" + NL + "2026-11-02_08-00-00  [local]" + NL + "2026-11-01_12-00-00  [cloud]" + NL + "2026-11-01_12-00-00_12  [local]"
[Fact] public void Test_DownloadByName_NoCurrentDevice()        // "Not in a device directory. cd into a camera or doorbell first."
[Fact] public void Test_DownloadByName_NoListingYet()           // cd into Pergoda, no List() call -> "Run LS first."
[Fact] public void Test_DownloadByName_SwitchingDeviceInvalidatesCache()
// cd Pergoda, List(), cd .., cd "Garage" (or any other device), DownloadByName("current") -> "Run LS first."
[Fact] public void Test_DownloadByName_NameNotFound()           // after List(), DownloadByName("nope") -> "'nope' wasn't in the last LS output."
[Fact] public void Test_DownloadByName_Current_Successful()
// after List(), DownloadByName("current", mediaDirectory: scratchDir) -> "Saved image to ...", file exists
[Fact] public void Test_DownloadByName_Cloud_Successful()
// DownloadByName("2026-11-01_12-00-00", mediaDirectory: scratchDir) -> "Saved video to ...", file exists
[Fact] public void Test_DownloadByName_Local_Successful()
// DownloadByName("2026-11-02_08-00-00", pollInterval: TimeSpan.Zero, maxRetries: 1, mediaDirectory: scratchDir) -> "Saved video to ...", file exists
```
Follow the existing `Test_ImageSuccessful`/`Test_DownloadSuccessful` pattern
for scratch directories (`Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`,
deleted in a `finally`).

- [ ] **Step 12: Run to verify failure**

Run: `dotnet test --filter UnitTest_ShellSession`
Expected: FAIL — device-level `List`/`DownloadByName` aren't implemented.

- [ ] **Step 13: Implement device-level `List` and `DownloadByName`**

Add `DeviceMediaListing? _lastDeviceMediaListing` and
`string? _lastDeviceMediaListingFor` fields, cleared (`null`) whenever
`ChangeDirectory` changes `_currentDeviceName` (including moving to a
different device or leaving the device level entirely). `List()`'s
device-level branch: `_lastDeviceMediaListing = library.ListDeviceMedia(_currentDeviceName!, _currentCamera, _currentDoorbell, pollInterval, maxRetries); _lastDeviceMediaListingFor = _currentDeviceName; return _lastDeviceMediaListing.ToListingString();`.
`DownloadByName(name, pollInterval, maxRetries, mediaDirectory)`: not-in-device
check first; then `_lastDeviceMediaListing is null || _lastDeviceMediaListingFor != _currentDeviceName`
→ `"Run LS first."`; then find the entry by name (case-insensitive) →
`"'<name>' wasn't in the last LS output."` if missing; otherwise dispatch on
`entry.Kind` to the matching `library.Download*` helper from Task 3 (`Current`
→ `DownloadCurrentThumbnail(_currentDeviceName!, _currentCamera, _currentDoorbell, mediaDirectory)`;
`Cloud` → `DownloadCloudItem(entry.CloudItem!, mediaDirectory)`; `Local` →
`DownloadLocalClip(entry.SyncModule!, entry.ManifestId!.Value, entry.LocalClip!, pollInterval, maxRetries, mediaDirectory)`).

- [ ] **Step 14: Run to verify pass**

Run: `dotnet test --filter UnitTest_ShellSession`
Expected: PASS; full suite green.

- [ ] **Step 15: Commit**

```bash
git add BlinkCameraCall/ShellSession.cs BlinkCameraCall/Extensions/DeviceMapExtension.cs \
  BlinkCameraCallUnitTests/UnitTest_ShellSession.cs
git commit -m "Add ShellSession for hub/device tree navigation, trigger, and download-by-name"
```

---

## Task 5: Wire up `Program.cs` and update documentation

**Files:**
- Modify: `BlinkCameraCall/Program.cs`
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/DEVELOPER.md`

**Interfaces:**
- Consumes (Task 1): bare-positional parsing, `ICommandAndArguments.BareDownload`.
  (Task 2): `ShellLineEditor`, `SystemConsoleIO`. (Task 4): `ShellSession`'s
  full public surface.

- [ ] **Step 1: Construct `ShellLineEditor` and `ShellSession`, and read lines through the editor**

Alongside the existing `blinkLibrary` construction:
```csharp
var shellLineEditor = new ShellLineEditor(new SystemConsoleIO());
var shellSession = new ShellSession(blinkLibrary);
```
Replace `Console.ReadLine() ?? string.Empty` with
`shellLineEditor.ReadLine(shellSession.Prompt)`.

- [ ] **Step 2: Add `cd`/`ls`/`trigger` switch cases and branch `download` on `BareDownload`**

```csharp
case "cd": Console.WriteLine(shellSession.ChangeDirectory(command.Arguments![0])); break;
case "ls": Console.WriteLine(shellSession.List()); break;
case "trigger": Console.WriteLine(shellSession.Trigger()); break;
case "download":
    Console.WriteLine(command.BareDownload
        ? shellSession.DownloadByName(command.Arguments![0])
        : blinkLibrary.Download(command.Arguments));
    break;
```
(Replaces the existing single `case "download":` line.)

- [ ] **Step 3: Update the `HELP` output**

Add, matching the existing lines' column alignment:
```
CD <name>|..|\                   Changes directory between hubs and devices.
LS                                Lists hubs, devices, or a device's media, depending on where you are.
TRIGGER                          Takes a fresh snapshot of the current device (cd into one first).
DOWNLOAD <name>                  Downloads a media item by the name LS showed (device directory only).
```

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS — this task adds no new automated tests of its own
(`Program.cs`'s `switch`/`Main` has never been unit-tested, consistent with
every prior command added here); it must simply still compile and the rest
of the suite stays green.

- [ ] **Step 5: Update `docs/USER_GUIDE.md`**

Add `CD`, `LS`, `TRIGGER`, and the bare `DOWNLOAD <name>` form to the commands
table/intro. Add a new `## Navigating with CD/LS` section (mirroring the
existing `## Device map (MAP)` section's style) covering: the `BC>` prompt
and how it grows; `cd <hub>`, `cd <device>`, `cd ..`, `cd \`; what `ls` shows
at each level; that `trigger`/bare `download` only work inside a device
directory and require `ls` to have been run first for `download`; that the
legacy flag commands (including `DOWNLOAD -i<id>`) still work unchanged from
anywhere. Extend the example session with a short `cd`/`ls`/`trigger`/`download`
walkthrough.

- [ ] **Step 6: Update `docs/DEVELOPER.md`**

Add `### 7.7 The shell navigation layer (CD/LS/TRIGGER, bare DOWNLOAD)` under
section 7 (mirroring `### 7.1 The MAP command`'s style), covering: hubs are
`Network`s not `SyncModule`s and why; the bare-positional parse path added
alongside the existing `" -"`-splitting one; `ShellLineEditor`/`IConsoleIO`'s
testability seam; the `ShellSession` cache invalidation rule for
`download`-by-name. Add bullets to `## 10. Known limitations / honest gaps`
for: no persisted command history across runs; no tab-completion; a
device-directory `ls` can be slow (it polls the sync module manifest exactly
like `LOCALCLIPS` does); the line editor's redraw assumes a stable
`Console.WindowWidth` for the duration of one keystroke and may misplace the
cursor on a mid-line terminal resize.

- [ ] **Step 7: Commit**

```bash
git add BlinkCameraCall/Program.cs docs/USER_GUIDE.md docs/DEVELOPER.md
git commit -m "Wire up CD/LS/TRIGGER and bare DOWNLOAD, and document the shell navigation layer"
```
