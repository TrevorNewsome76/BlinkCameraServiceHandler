# ARM/DISARM Reliability and Live Feedback — Design Spec

## Context

A real-world `ARM` at the root (whole system, 3 hubs / 16 devices) produced
a console full of `"Blink rejected the request: ..."` lines and a final
`"Armed the system (0/16 succeeded across 3 hubs)."` — yet the devices
appeared to actually arm. A spike (reproducing the exact logged response
through this codebase's real deserialization path) found the true cause,
which turned out to be unrelated to concurrency:

- Blink's real response to `arm`/`disarm`/`enable`/`disable` is a large
  "command record" object (`id`, `created_at`, `state_condition`,
  `sync_module_id`, …) — nothing like the flat
  `{id, network_id, complete, status_code}` shape every existing mock
  fixture assumed.
- This codebase's generic `Deserialize<T>` (`JsonSerializerExtension.cs`),
  which goes through `Shadow.Quack`'s `JsonProxy`, throws a
  `FormatException` on that real shape (reproduced directly; the failure
  is some interaction effect across the combined set of fields, not any
  single one) and **silently swallows the exception**, returning an
  all-default (`Id = 0`, `Complete = false`, …) object instead.
- `TriggerSnapshot` (the private helper behind `SNAP`, both network
  arm/disarm, all four enable/disable methods, and
  `SystemPrepareLocalStorageClip`) then sees `Id == 0` and concludes
  "Blink rejected the request," printing the entire real, *successful*
  response back as if it were a rejection — and never even starts polling
  for completion, since it throws before that point.
- Plain `System.Text.Json`, by contrast, parses the identical real payload
  correctly (`Id = 3796763377`, confirmed by the spike).

Separately, the **camera/doorbell** lines in that same log really were
rejected — with a distinguishable real signal, `{"message":"System is
busy, please wait","code":307}` — fired immediately after the (apparently
failed, actually successful) hub arm call with no pacing at all. This looks
like genuine Blink-side rate-limiting, not a bug in this codebase, but
retrying briefly would very likely ride it out.

Finally, independent of both of the above: the whole operation builds one
giant string in memory and only hands it to the console after every single
device has been attempted, which is why a 16-device system arm looks and
feels like the app has locked up, with no way to tell it to stop.

Per the brainstorming discussion that preceded this spec, the user chose to
fix all three in this one branch: (1) the false-rejection parsing bug
(critical — it's actively lying about success/failure), (2) retry/backoff
for genuine busy (307) rejections, (3) live per-item feedback, a "please
wait" spinner, and the ability to interrupt.

## Problem

1. Every command that goes through `TriggerSnapshot` (or
   `SystemPrepareLocalStorageClip`'s own copy of the same pattern) is
   silently misreported as rejected whenever Blink's real response is
   richer than the mocks ever anticipated — which, per the one real log
   available, is apparently *always*, for *every* command. No completion
   polling ever runs for a real account today.
2. A genuine, real rate-limit rejection (307 "system busy") is treated as
   instantly fatal for that device, with no retry, even though it looks
   transient.
3. A multi-device `ARM`/`DISARM` gives zero feedback until completely
   finished, and cannot be stopped once started.

## Goals

- Fix the parsing bug at its root for every caller of the shared
  `ICommandStatusResponse` shape (`TriggerSnapshot`'s 8 callers *and*
  `SystemPrepareLocalStorageClip`, which independently duplicates the same
  broken pattern against the same interface).
- When a single device/hub call is rejected specifically with Blink's
  "system busy" signal (`"code":307`), retry up to 3 total attempts with a
  short increasing delay (~1s, then ~2s) before giving up on that one item
  — any other rejection (e.g. `"code":402` "Sync Module Offline") still
  fails immediately, exactly as today.
- Every per-item line (hub's own line, each camera's, each doorbell's)
  prints to the console the instant it's known, not batched to the end.
- A "Sending instructions, please wait…" message with an animated spinner
  shows for the duration of *any* `ARM`/`DISARM` (all three levels — a
  single device can now take a few seconds too, if it needs to retry
  through a busy rejection).
- Pressing Escape (or Ctrl+C, read as a plain key — it must not kill the
  process) during the operation lets whatever's already in flight finish,
  then stops before starting anything new, and prints a summary of
  whatever was completed before stopping.
- None of this changes behavior for anyone not hitting these paths: all
  234 existing tests keep passing unmodified.

## Non-goals / known risks

- **`SystemRequestLocalStorageManifest` and `ManifestPolling`'s response
  shapes (`IManifestRequestResponse`, `ILocalStorageManifestResult`) are
  not touched.** They duplicate a similar "deserialize, check an id,
  throw" pattern, but against *different* interfaces this spec has no
  real-world evidence is actually broken. Fixing them would need their own
  investigation; flagged here as a likely follow-up, not addressed now.
- **No per-retry console output.** A busy-retry happens silently inside
  the API layer; only the final outcome (success, or failure after 3
  attempts) produces a line. Threading progress callbacks all the way down
  into `TriggerSnapshot` to report each retry attempt would mean adding an
  `Action<string>` parameter to every one of the six ARM/DISARM API
  methods (and `SNAP`'s, since they share the helper) — a much larger
  change for a cosmetic nicety. Not worth it here.
- **Cancellation never aborts an HTTP call already sent.** It's checked
  between devices and before each busy-retry wait, never mid-flight of a
  single request — per the user's own choice ("finish whatever's
  in-flight, then stop").
- **No arbitration between the new spinner and `WATCH`'s existing
  background alert sink.** If a `WATCH` alert fires while an `ARM`/`DISARM`
  busy-loop is running, `ShellLineEditor.NotifyExternal` takes its
  `!_active` branch (since `ReadLine` isn't running then either) and
  writes directly — which can interleave messily with the spinner's own
  redraw. This already existed as a narrower risk before this change (any
  direct `Console.Write` during that window); building a single arbiter
  across `ShellLineEditor`, the alert sink, and the new spinner is a much
  bigger change than this bugfix. Accepted, not solved.
- **The exact field-combination that breaks `Shadow.Quack`'s `JsonProxy`
  was not fully isolated.** The spike bisected several subsets of the real
  payload (each of which parsed fine alone) without finding a single
  culprit field — bypassing the library for this one interface, rather
  than patching or fully understanding it, is the chosen fix.

## Design

### 1. The parsing fix

New file, `BlinkCommon/Extensions/CommandStatusJsonExtension.cs`:

```csharp
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
                Complete = root.TryGetProperty("complete", out var c) && c.ValueKind == JsonValueKind.True,
                Status_Code = ReadInt(root, "status_code"),
            });
        }
        catch
        {
            return Duck.Implement<ICommandStatusResponse>(new { });
        }
    }

    private static long ReadLong(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.TryGetInt64(out var v) ? v : 0;

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.TryGetInt32(out var v) ? v : 0;
}
```

This uses plain `System.Text.Json` only — no `Shadow.Quack.Json.JsonProxy`
anywhere in the path — and preserves the existing "missing field defaults
to 0/false, total parse failure defaults to an all-empty object" contract
the rest of the codebase already relies on, just via a mechanism proven
(by the spike) to actually work on Blink's real response shape.

Three call sites switch from `.Deserialize<ICommandStatusResponse>()` to
`.ParseCommandStatus()`:
- `BlinkApiTransactions.TriggerSnapshot`'s initial POST response check.
- `CommandPolling.WaitForCommandComplete`'s poll response check.
- `BlinkApiTransactions.SystemPrepareLocalStorageClip`'s initial POST
  response check (it independently duplicates `TriggerSnapshot`'s pattern
  against the same interface, so it gets the same fix).

### 2. Busy-retry

New private helper in `BlinkApiTransactions.cs`:

```csharp
private static bool IsBusyRejection(string rawResponse)
{
    try
    {
        using var doc = JsonDocument.Parse(rawResponse);
        return doc.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var value) && value == 307;
    }
    catch
    {
        return false;
    }
}
```

`TriggerSnapshot` gains a retry loop around the initial POST, plus an
optional trailing `CancellationToken cancellationToken = default`:

```csharp
private static ICommandStatusResponse? TriggerSnapshot(string triggerUrl, long networkId, string tier, TimeSpan? pollInterval, int maxRetries, CancellationToken cancellationToken = default)
{
    if (ApiDriver is null)
        return null;

    var busyDelays = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) };
    string rawTrigger = string.Empty;
    ICommandStatusResponse triggerResult = Duck.Implement<ICommandStatusResponse>(new { });

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

(3 total POST attempts: the initial one plus two retries, at ~1s then
~2s — matching "3 attempts, short increasing delay.")

Only the six ARM/DISARM methods (`IApiTransactions`, `BlinkApiTransactions`,
`AdapterExtension`) gain a new trailing optional
`CancellationToken cancellationToken = default` parameter, forwarded into
`TriggerSnapshot`. `SNAP`'s two existing public methods
(`SystemTriggerCameraSnapshot`/`SystemTriggerDoorbellSnapshot`) are **not**
changed — they simply don't pass one, so `TriggerSnapshot`'s own default
applies, and they benefit from the busy-retry automatically for free
(same shared helper), with zero change to their existing signatures or
tests.

### 3. Live streaming, spinner, and interrupt

**`BlinkLibrary` changes** — every existing public `Arm`/`Disarm`
signature gains new *trailing* optional parameters (safe: every current
positional test call already stops before these, so nothing existing
breaks):

```csharp
public string Arm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120, long? networkId = null, CancellationToken cancellationToken = default)
public string Disarm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120, long? networkId = null, CancellationToken cancellationToken = default)

public string Arm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
public string Disarm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default)

public string Arm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
public string Disarm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
```

(Device-level takes only a `CancellationToken`, no `onProgress` — it only
ever produces one line, at the very end, so there's nothing to stream.)

`ArmOrDisarmNetwork`'s private signature grows a `Cancelled` flag in its
return tuple, plus the new parameters:

```csharp
private (List<string> Lines, int Succeeded, int Total, bool Cancelled) ArmOrDisarmNetwork(IHomeScreenNetwork network, bool arm, TimeSpan? pollInterval, int maxRetries, Action<string>? onProgress, CancellationToken cancellationToken)
```

Every line it currently does `lines.Add(...)` for is instead produced
through one small local helper:

```csharp
void Emit(string line) { lines.Add(line); onProgress?.Invoke(line); }
```

Before the network's own attempt, and before each camera/doorbell in the
two existing `foreach` loops, a check is added:

```csharp
if (cancellationToken.IsCancellationRequested)
    return (lines, succeeded, total, true);
```

Each item's existing `try { ... } catch (Exception ex) { Emit(...); }`
gains a `catch (OperationCanceledException)` arm *before* the general
`catch (Exception ex)` (since `OperationCanceledException` is an
`Exception` and would otherwise be swallowed as an ordinary per-item
failure, letting the loop wrongly continue to the next item instead of
stopping):

```csharp
catch (OperationCanceledException)
{
    return (lines, succeeded, total, true);
}
```

**Hub-level `Arm`/`Disarm`**: when `Cancelled` comes back true, the
closing line changes from the normal
`"Armed hub '<name>' (x/y succeeded)."` to
`"Arming hub '<name>' cancelled (x/y attempted)."` (or "Disarming…"), and
this line is itself passed to `onProgress` too.

**System-level `ArmOrDisarmSystem`**: the `foreach` over networks checks
the returned `Cancelled` flag after each network; on the first cancelled
network, it emits that network's own cancelled line (as above), stops
iterating over any remaining networks, and the final line becomes
`"Arming the system cancelled (x/y attempted across N of M hubs)."`
instead of the normal "succeeded across M hubs" line — where `N` is the
count of hubs actually reached (including the one that got cancelled
mid-way) and `M` is the total.

**Device-level `ArmOrDisarm`**: a `cancellationToken.ThrowIfCancellationRequested()`
is checked at the top, and `OperationCanceledException` from the adapter
call is caught separately, returning
`"Arming '<name>' cancelled."` (or "Disarming…") instead of the normal
success/timeout/rejected message.

**`ShellSession.Arm`/`Disarm`** gain the identical new trailing parameters,
threaded straight through to whichever `BlinkLibrary` overload they
already dispatch to — zero new business logic, same as today.

**New `IConsoleIO` member** (`BlinkCameraCall/IO/IConsoleIO.cs`):

```csharp
bool KeyAvailable { get; }
```

`SystemConsoleIO.KeyAvailable => Console.KeyAvailable;` — a non-blocking
check, alongside the existing blocking `ReadKey()`.

**New `BlinkCameraCall/IO/ProgressSpinner.cs`**, built on `IConsoleIO`
(same testability seam `ShellLineEditor` already uses):

```csharp
public class ProgressSpinner(IConsoleIO io)
{
    private static readonly char[] Frames = ['|', '/', '-', '\\'];
    private readonly object _lock = new();
    private int _frameIndex;
    private bool _active;

    public void Start(string message) { /* writes message + " ", draws first frame */ }
    public void Tick() { /* erases + redraws the next frame; no-op if not active */ }
    public void WriteLine(string text) { /* erases frame, writes text + newline, redraws frame if still active */ }
    public void Stop() { /* erases the frame, marks inactive */ }
}
```

All four methods take the same `_lock`, since `Tick()` runs from the
console-loop thread while `WriteLine` is invoked from the background
`Task`'s thread via `onProgress`. Unlike `ShellLineEditor`'s redraw math,
this only ever moves the cursor back by the one trailing spinner
character, so no multi-row wrap handling is needed.

**`Program.cs` wiring** — the `"arm"`/`"disarm"` cases become:

```csharp
case "arm":
case "disarm":
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        Console.WriteLine(command.Command == "arm" ? shellSession.Arm() : shellSession.Disarm());
        break;
    }

    var cts = new CancellationTokenSource();
    progressSpinner.Start("Sending instructions, please wait...");

    var task = Task.Run(() => command.Command == "arm"
        ? shellSession.Arm(onProgress: progressSpinner.WriteLine, cancellationToken: cts.Token)
        : shellSession.Disarm(onProgress: progressSpinner.WriteLine, cancellationToken: cts.Token));

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
    Console.WriteLine(task.Result);
    break;
}
```

(`progressSpinner` is one `ProgressSpinner` instance constructed once at
startup alongside the existing `shellLineEditor`, sharing a
`SystemConsoleIO`.) The redirected-input/output branch keeps today's
exact behavior unchanged — matches the same split `WATCH`'s `alertSink`
already uses.

### Error handling summary

| Condition | Output |
|---|---|
| Real command accepted, Blink's rich response now parses correctly | Polling proceeds exactly as it was always meant to — this is the headline fix. |
| Busy (307) rejection, retried, eventually accepted | Succeeds silently after the retry; same final per-item line as any other success. |
| Busy (307) rejection, still busy after 3 attempts | That item's line: `"Blink rejected the request: <raw>"`, same as today — operation continues to the next item. |
| Any other rejection (e.g. 402 "Sync Module Offline") | Fails immediately, no retry — unchanged from today. |
| Cancelled between items | That level's closing line says "...cancelled (x/y attempted)." instead of "...succeeded." — operation stops, nothing further is attempted. |
| Cancelled mid-retry-wait for one item | That one item's line: `"...cancelled."` — operation stops there. |

### Testing

- **`CommandStatusJsonExtension`** — new unit tests: the real logged
  payload (or an equivalently rich synthetic one) parses `Id` correctly;
  a response missing `complete`/`status_code` entirely still defaults
  those to `false`/`0` without throwing; a totally malformed string
  returns the existing all-default fallback; the existing simple flat
  shape (`{"id":1,...}`) still parses identically to before (proving no
  behavior change for every existing mock fixture).
- **`IsBusyRejection`** — new unit tests: `{"code":307,...}` → true;
  `{"code":402,...}` → false; malformed JSON → false (no throw).
- **`TriggerSnapshot` retry** — exercised indirectly through
  `BlinkLibrary`/`MockAdapter` (this codebase's established pattern, same
  as the original ARM/DISARM feature): a fixture sequence of
  busy/busy/success proves eventual success after 2 retries; a fixture
  sequence of busy/busy/busy proves failure after exactly 3 attempts
  (assert call count, not just the outcome); a fixture returning a
  non-busy rejection (`402`) proves no retry happens at all (assert call
  count is 1).
- **Cancellation** — hub-level: a fixture where the first camera's call
  blocks until cancelled (or simply: cancel before calling, proving
  nothing is attempted and the cancelled-summary line is correct);
  system-level: cancelling partway through the first of two hubs proves
  the second hub is never attempted; device-level: cancelling before the
  call proves the `"...cancelled."` message.
- **`ProgressSpinner`** — new `UnitTest_ProgressSpinner.cs` using the
  existing `FakeConsoleIO`: `Start` writes the message once; `Tick`
  overwrites in place (same write-count-and-cursor-math style assertions
  `UnitTest_ShellLineEditor` already uses); `WriteLine` while active
  erases the frame, writes the line, and redraws; `WriteLine` while
  inactive just writes the line; `Stop` erases cleanly and further
  `Tick()` calls become no-ops.
- **`ShellSession`/`Program.cs` wiring** — the existing dispatch tests
  (`Test_Arm_AtRoot_DelegatesToSystemWide`, etc.) are re-verified to still
  pass unmodified with the new trailing default parameters; no new
  `Program.cs`-level test is added (this project has never had direct
  `Program.cs` tests — the busy-loop itself is thin orchestration in the
  same spirit as the rest of `Main`).
