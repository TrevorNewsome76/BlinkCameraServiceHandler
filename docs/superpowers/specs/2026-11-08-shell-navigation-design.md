# Interactive Shell Navigation & Line Editor — Design Spec

## Context

The terminal currently reads one line at a time with `Console.ReadLine()` and
dispatches a flat set of flag-style commands (`DEVICE -n<name>`, `SNAP -n<name>`,
`VIDEOS -h<hours>`, etc. — see
[2026-11-03-device-detail-and-media-design.md](2026-11-03-device-detail-and-media-design.md)).
Every command that targets a device repeats the device's name as a flag on
every call, and there is no sense of "where you are."

The user wants the terminal to feel like a PowerShell session: a prompt that
shows a path (`BC>`), a protected prompt you can't backspace into, left/right/
home/end cursor movement and up/down command history while typing, and
`cd`/`ls` navigation through a tree of hubs → devices, with `trigger` and
`download` acting on whatever device you're currently "in."

## Problem

There's no persistent sense of context. Every operation on a device repeats
its full name, there's no way to browse what's available without already
knowing exact names (short of re-reading `MAP`'s full dump), and the input
line itself is a bare `Console.ReadLine()` with none of the editing niceties
a real shell has.

## Goals

- A prompt that reflects current position in a two-level tree — root, a hub
  (network), or a device (camera/doorbell) — and grows/shrinks as you `cd`.
- `cd <name>`, `cd ..`, `cd \` (or `/`) to move through that tree.
- `ls` lists whatever is "in" the current directory: hubs at root, devices
  within a hub, merged media within a device.
- `trigger` (device directory only) takes a fresh snapshot of the current
  device — the tree equivalent of `SNAP -n<name>`.
- `download <name>` (device directory only) downloads a merged-listing entry
  by the name `ls` showed for it.
- A custom line editor: prompt text can never be deleted or cursored into;
  Left/Right/Home/End move within the typed input; Up/Down recall this
  session's command history.
- All of the above sit **alongside** the existing flag-style commands, which
  keep working unchanged from any directory level (including the legacy
  `DOWNLOAD -i<id>`).

## Non-goals / known risks

- **No persisted history across runs** — history is in-memory for the
  session only, matching how `_sessionDetails`/caches already reset on
  restart.
- **No tab-completion.** Only the editing/navigation keys listed above.
  Nothing here rules it out later, but it wasn't asked for.
- **Hubs are `Network`s, not `SyncModule`s.** A network's directly-attached
  cameras/doorbells all appear under it even if some don't route through a
  sync module at all (Blink Minis use none and also stay unmodeled per the
  prior spec's non-goals — they just won't appear in `ls` output).
- **Device-directory `ls` can be slow.** It polls the parent sync module's
  manifest the same way `LOCALCLIPS` does today (up to 120×1s). This is
  accepted, not hidden — see the "Local SD clips" step below.
- **No delete-from-card, no liveview** — unchanged from the prior spec; not
  revisited here.
- **No raw-terminal resize handling beyond what `Console` already gives us.**
  The redraw logic assumes `Console.WindowWidth` is stable for the duration
  of a single keystroke's redraw; a resize mid-line may misplace the cursor
  until the next full prompt redraw. Not solved here — PowerShell itself is
  not perfectly robust to this either.

## Design

### Component overview

Three new pieces, all in `BlinkCameraCall`:

- **`IConsoleIO` + `SystemConsoleIO`** (new, `IO/`) — the only seam between
  the editor and the real console.
- **`ShellLineEditor`** (new, `IO/`) — owns the buffer/cursor/history state
  machine; reads through `IConsoleIO`.
- **`ShellSession`** (new, top-level) — owns navigation state (current
  network/device) and the device-directory media cache; wraps a
  `BlinkLibrary` instance (constructor-injected, same pattern `BlinkLibrary`
  itself uses for `IAdapter`).

`BlinkLibrary` gains one small addition so `ShellSession` can read the
already-fetched device lists without duplicating them:

```csharp
public ISessionDetails? CurrentSession => _sessionDetails.LoggedInStatus ? _sessionDetails : null;
```

Everything else about `BlinkLibrary` is unchanged — it remains the only
thing that talks to `IAdapter`.

### `IConsoleIO`

```csharp
public interface IConsoleIO
{
    ConsoleKeyInfo ReadKey();
    void Write(string text);
    int CursorLeft { get; set; }
    int CursorTop { get; set; }
    int WindowWidth { get; }
}
```

`SystemConsoleIO` implements each member by calling the matching
`System.Console` member/method directly (`Console.ReadKey(intercept: true)`,
etc.).

### `ShellLineEditor`

```csharp
public class ShellLineEditor(IConsoleIO io)
{
    public string ReadLine(string prompt);
}
```

Behavior of `ReadLine`:

1. `io.Write(prompt)`. Record `promptEndLeft`/`promptEndTop` = the cursor
   position immediately after writing it (via `io.CursorLeft`/`CursorTop`).
   This is the one fixed point the buffer can never move left of.
2. Maintain `buffer` (`StringBuilder`) and `cursorIndex` (`int`, `0..buffer.Length`,
   relative to the start of the buffer — *not* an absolute console column).
3. Loop on `io.ReadKey()`:
   - **Printable character**: insert into `buffer` at `cursorIndex`, increment
     `cursorIndex`, redraw from `cursorIndex - 1` to `buffer.Length` (handles
     wrap by recomputing the absolute row/column from `promptEndLeft/Top`,
     `buffer`'s length, and `io.WindowWidth` — never assumes a single row).
   - **Backspace**: no-op if `cursorIndex == 0`; otherwise remove the char at
     `cursorIndex - 1`, decrement `cursorIndex`, redraw tail (including
     erasing the now-vacated final character position).
   - **Delete**: no-op if `cursorIndex == buffer.Length`; otherwise remove the
     char at `cursorIndex`, redraw tail.
   - **LeftArrow**: `cursorIndex = Math.Max(0, cursorIndex - 1)`; move the
     console cursor only (no redraw needed).
   - **RightArrow**: `cursorIndex = Math.Min(buffer.Length, cursorIndex + 1)`.
   - **Home**: `cursorIndex = 0`.
   - **End**: `cursorIndex = buffer.Length`.
   - **UpArrow**: if not already at the oldest history entry, step one entry
     older; replace `buffer`'s contents with that entry, `cursorIndex = buffer.Length`,
     full tail redraw. The in-progress buffer is discarded when you first press
     Up (no draft-saving — simplest behavior, matches the ask).
   - **DownArrow**: symmetric; stepping past the newest history entry clears
     the buffer back to empty.
   - **Enter**: write a newline, and return `buffer.ToString()`. If non-empty,
     append it to the in-memory history list (skip exact-duplicate of the
     immediately previous entry, matching common shell behavior, so repeatedly
     hitting Enter on blank doesn't spam history — blank entries are never
     appended at all since `Enter` only appends non-empty buffers).
4. All redraws write the *entire remaining tail* from the edit point rather
   than trying to patch individual characters, then reposition the cursor —
   simpler and correct for the moderate line lengths this app deals with.

### `ShellSession`

```csharp
public class ShellSession(BlinkLibrary library)
{
    public string Prompt { get; }                 // "BC>", "BC\\<hub>>", "BC\\<hub>\\<device>>"

    public string ChangeDirectory(string target);  // "cd" verb
    public string List();                          // "ls" verb
    public string Trigger();                       // "trigger" verb
    public string DownloadByName(string name);      // bare "download <name>"
}
```

State: `IHomeScreenNetwork? _currentNetwork`, and
`(IHomeScreenCamera? Camera, IHomeScreenDoorbells? Doorbell)? _currentDevice`.
`Prompt` is computed from these two fields plus the fixed `"BC"` root segment.

**`ChangeDirectory(target)`**

- `library.CurrentSession is null` → `"Not currently logged into Blink service."`
- `target` is empty/whitespace (bare `cd` with nothing after it): same as `cd \` — goes straight to root.
- `target` is `".."`: if in a device, drop to hub (`_currentDevice = null`); if
  in a hub, drop to root (`_currentNetwork = null`); at root, no-op (silently —
  matches `cd ..` at a filesystem root doing nothing).
- `target` is `"\\"` or `"/"`: `_currentNetwork = null; _currentDevice = null`.
- Otherwise, resolve `target` against the current level:
  - At root: match `library.CurrentSession.Networks` by name
    (case-insensitive). No match → `"No hub named '<target>' was found. Run LS to see exact names."`
  - In a hub: match that network's cameras/doorbells by name (same
    ambiguity-as-null rule `FindCameraByName`/`FindDoorbellByName` already
    use). No match → `"No device named '<target>' was found. Run LS to see exact names."`
  - In a device: `cd` to another name isn't supported two levels down in one
    step — same "no match" message (there's nothing to resolve a device name
    against from inside another device).
- On success, update state and return the new prompt-equivalent confirmation,
  e.g. `"Now in hub 'Home Front'."` / `"Now in device 'Front Door'."` / `"Back to root."`

**`List()`**

- At root: one line per network, reusing `DeviceMapExtension`'s existing
  `$"Network: {name} ({armed/disarmed})"` formatting for consistency.
- In a hub: one line per camera/doorbell in that network, reusing the
  existing `FormatCameraLine`/`FormatDoorbellLine`-equivalent detail (these
  two helpers move from `private` to `internal` in `DeviceMapExtension` so
  `ShellSession` can call them instead of duplicating the format strings).
- In a device: builds and caches the merged media listing (see "Device media
  listing" below), then prints it.

**`Trigger()`**

- No current device → `"Not in a device directory. cd into a camera or doorbell first."`
- Otherwise delegates to the existing `library.Snap(new[] { "n" + currentDeviceName })`
  — same array shape `ExtractDeviceName` already expects — and returns its
  result unchanged. No new Snap logic; this is purely "imply the name from
  context."

**`DownloadByName(name)`**

- No current device → `"Not in a device directory. cd into a camera or doorbell first."`
- No cached listing for the current device (i.e. `ls` hasn't been run since
  `cd`-ing here, or since the last `ls`) → `"Run LS first."`
- Name not in the cached listing → `"'<name>' wasn't in the last LS output."`
- Otherwise dispatch by the cached entry's kind:
  - `current` → same thumbnail-URL-building + `GetBytes` +
    `SaveMediaFile(name, bytes, "jpg")` steps `BlinkLibrary.Image` already
    does (factored into a small internal helper on `BlinkLibrary` so neither
    copy duplicates the URL-building logic: `internal string DownloadCurrentThumbnail(string deviceName)`,
    called by both `Image(string[])` and this path).
  - `[cloud]` entry → same `GetBytes({tier-base}{item.Media})` +
    `SaveMediaFile(item.Device_Name, bytes, "mp4")` steps `BlinkLibrary.Download`
    already does, factored the same way:
    `internal string DownloadCloudItem(ICloudMediaItem item)`.
  - `[local]` entry → same prepare-then-download steps `BlinkLibrary.LocalDownload`
    already does, factored the same way:
    `internal string DownloadLocalClip(IHomeScreenSyncModule syncModule, long manifestId, ILocalStorageClip clip)`.

Factoring these three bodies out of `Image`/`Download`/`LocalDownload` into
internal helpers (called by both the existing flag commands and
`ShellSession`) avoids duplicating the actual network/URL logic — the public
methods become thin wrappers: resolve name/id → validate → call the internal
helper → return its message.

### Device media listing (the `ls`/`download` data behind a device)

New method on `BlinkLibrary`:

```csharp
internal DeviceMediaListing ListDeviceMedia(string deviceName, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell)
```

returning a new small type:

```csharp
internal record DeviceMediaListing(List<DeviceMediaEntry> Entries);
internal record DeviceMediaEntry(string Name, DeviceMediaKind Kind, ICloudMediaItem? CloudItem, ILocalStorageClip? LocalClip, IHomeScreenSyncModule? SyncModule, long? ManifestId);
internal enum DeviceMediaKind { Current, Cloud, Local }
```

Building it:

1. **Current thumbnail**: if `camera?.Thumbnail ?? doorbell?.Thumbnail` is
   non-empty, one entry named `"current"`, kind `Current`.
2. **Cloud events**: call the same `adapter.GetRecentMedia(account, DateTime.UtcNow.AddHours(-24))`
   `VIDEOS` already calls, filter to `Device_Name` matching (case-insensitive)
   `deviceName`, excluding `Deleted`. Each becomes an entry named from
   `Created_At` formatted `yyyy-MM-dd_HH-mm-ss`, kind `Cloud`.
3. **Local SD clips**: find a sync module whose `Network_Id` matches the
   device's `Network_Id`. If none, or its `Local_Storage_Status != "active"`,
   skip this step entirely (no error, no entries — see Non-goals). Otherwise
   run the same request/poll `LocalClips` already does, filter clips whose
   `Camera_Name` matches `deviceName` (case-insensitive), each becoming an
   entry named from `Created_At`, kind `Local`.
4. **Collision handling**: if two entries would share a name, append `_<id>`
   (the cloud item's or local clip's numeric id) to the second one found.
5. Cache the resulting `DeviceMediaListing` on `ShellSession` (not
   `BlinkLibrary` — it's navigation-session state), keyed by device name, so a
   following `DownloadByName` can use it without re-fetching. A fresh `ls`
   call always replaces the cache (never merges with a stale one).

Printed format for `ls` in a device directory: one line per entry,
`"<name>  [<kind>]"` (`kind` lowercased: `current`, `cloud`, `local`), sorted
newest-first by whatever timestamp each entry carries (the "current" entry,
having none, sorts first regardless).

### Command parsing (`CommandProcessorExtension`)

Add `"cd"`, `"ls"`, `"trigger"` to `ValidCommands`. Add a second parse
function, used **before** the existing flag-splitting path:

```csharp
private static readonly HashSet<string> BarePositionalCommands = new(StringComparer.OrdinalIgnoreCase) { "cd", "ls", "trigger" };

public static ICommandAndArguments ProcessCommandString(this string consoleCommand)
{
    if (string.IsNullOrEmpty(consoleCommand))
        return /* existing empty-command object, unchanged */;

    var firstSpace = consoleCommand.IndexOf(' ');
    var verb = (firstSpace < 0 ? consoleCommand : consoleCommand[..firstSpace]).Trim();
    var remainder = firstSpace < 0 ? string.Empty : consoleCommand[(firstSpace + 1)..].Trim();

    if (BarePositionalCommands.Contains(verb))
        return Duck.Implement<ICommandAndArguments>(new { Command = verb.ToLowerInvariant(), Arguments = new[] { remainder } }).ValidateCommand();

    if (string.Equals(verb, "download", StringComparison.OrdinalIgnoreCase) && remainder.Length > 0 && !remainder.StartsWith('-'))
        return Duck.Implement<ICommandAndArguments>(new { Command = "download", Arguments = new[] { remainder }, BareDownload = true }).ValidateCommand();

    return consoleCommand.FormatCommand(); // existing " -"-splitting path, untouched
}
```

(`BareDownload` is a new flag field on `ICommandAndArguments` — default
`false` — that `Program.cs` checks to decide which handler gets the bare
`Arguments[0]` name vs. the legacy flag-array shape. `ls`/`trigger`'s
`Arguments` array holding one possibly-empty string is harmless since neither
handler reads it.)

This leaves every existing flag-based command and every existing test for
`FormatCommand`/`CreateReturnObject` untouched — the new branch only
intercepts the four verbs that need it, before the old splitting logic ever
runs.

### `Program.cs` wiring

- Construct `var shellLineEditor = new ShellLineEditor(new SystemConsoleIO());`
  and `var shellSession = new ShellSession(blinkLibrary);` alongside the
  existing `blinkLibrary` construction.
- Replace `Console.ReadLine() ?? string.Empty` with
  `shellLineEditor.ReadLine(shellSession.Prompt)`.
- Add switch cases:
  - `"cd"` → `Console.WriteLine(shellSession.ChangeDirectory(command.Arguments![0]));`
  - `"ls"` → `Console.WriteLine(shellSession.List());`
  - `"trigger"` → `Console.WriteLine(shellSession.Trigger());`
  - `"download"` when `command.BareDownload` → `Console.WriteLine(shellSession.DownloadByName(command.Arguments![0]));`;
    otherwise falls through to the existing `blinkLibrary.Download(command.Arguments)` line unchanged.
- `HELP` output gains:
  ```
  CD <name>|..|\                   Changes directory between hubs/devices.
  LS                                Lists hubs, devices, or a device's media, depending on where you are.
  TRIGGER                          Takes a fresh snapshot of the current device (cd into one first).
  DOWNLOAD <name>                  Downloads a media item by the name LS showed (device directory only).
  ```

### Error handling summary

| Condition | Output |
|---|---|
| Not logged in, any tree command | `"Not currently logged into Blink service."` |
| `cd`/`ls` at root/hub with no `MAP`/`GET` run yet | `"Run MAP or GET first to load your devices."` |
| `cd` to unknown name | `"No hub named '<target>' was found. Run LS to see exact names."` or the device-level equivalent |
| `trigger`/bare `download` with no current device | `"Not in a device directory. cd into a camera or doorbell first."` |
| Bare `download` before `ls` run (or after `cd` elsewhere) | `"Run LS first."` |
| Bare `download` name not in cached listing | `"'<name>' wasn't in the last LS output."` |

### Testing

- **`UnitTest_ShellLineEditor`** (new) — a `FakeConsoleIO` implementing
  `IConsoleIO` from a queued `ConsoleKeyInfo[]` and recording every `Write`
  call plus cursor moves. Cases: plain typing returns the typed string;
  Backspace at the very start of input is a no-op (prompt never affected);
  Left/Right move without altering the buffer; insert-in-the-middle via
  Left+char; Home/End jump; Up recalls the previous entry, Down returns to
  empty; Enter on an empty buffer returns `""` and does not add a history
  entry; Enter does not duplicate the immediately-previous history entry.
- **`UnitTest_ShellSession`** (new) — built on the existing `MockAdapter`
  pattern used by `UnitTest_BlinkLibrary`. Cases: `Prompt` at each of the
  three levels; `cd <hub>` then `ls` lists that hub's devices; `cd ..` from
  device returns to hub, from hub to root, from root is a no-op; `cd \`
  from a device returns straight to root; `cd` to an unknown name at each
  level; `trigger`/bare `download` with no current device; `ls` in a device
  directory with a current thumbnail + mocked cloud events + mocked local
  clips merges and names all three correctly, including a collision case;
  `download <name>` immediately after that `ls` succeeds for a `current`,
  a `cloud`, and a `local` entry; `download` before any `ls` in the device
  returns `"Run LS first."`.
- **`UnitTest_CommandProcessorExtension`** additions — `cd Front Door` →
  command `cd`, `Arguments[0] == "Front Door"`; `download -i123` still
  parses via the existing flag path with `BareDownload` unset/false;
  `download Front Door Clip` parses as bare with `BareDownload == true`;
  `ls`/`trigger` with trailing text still parse as their verb (remainder is
  simply ignored by both handlers).
- **`UnitTest_BlinkLibrary`** additions — the three new `internal`
  `DownloadCurrentThumbnail`/`DownloadCloudItem`/`DownloadLocalClip` helpers
  are exercised indirectly through the existing `Image`/`Download`/`LocalDownload`
  tests (no behavior change expected there) and directly through the new
  `ShellSession` tests above — add `[InternalsVisibleTo("BlinkCameraCallUnitTests")]`
  to `BlinkCameraCall.csproj` so the test project can reach them without a
  public surface-area increase.
