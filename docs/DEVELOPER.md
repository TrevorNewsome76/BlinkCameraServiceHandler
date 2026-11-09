# Developer Guide: How BlinkCameraServiceHandler Is Built

This document explains the design of this repository in enough depth that it could be
rebuilt from scratch. It covers *what* each piece does, *why* it exists, and the
reasoning behind the less obvious decisions (especially around Blink's login flow,
which is the part most likely to break again when Blink changes something).

If you only want to use the program, see [USER_GUIDE.md](USER_GUIDE.md) instead.

## 1. What this program is

A Windows console application that logs into a personal Blink home-security account
and talks to Blink's cloud API: logging in (including two-factor auth), logging out,
verifying a client-trust PIN, and fetching the "home screen" (networks, sync modules,
cameras, doorbells). It is a thin, scriptable client — there is no GUI and no local
video handling; it is a REPL over Blink's REST/OAuth API.

## 2. Project layout

```
BlinkCommon/              Interfaces and small cross-cutting helpers. No logic that
                           talks to the network lives here - just contracts (C#
                           interfaces) and a JSON serialize/deserialize helper.
BlinkCameraCall/           The actual console application: Program.cs (the REPL),
                           the classes that implement BlinkCommon's interfaces, and
                           everything that makes an HTTP call.
BlinkCameraCallUnitTests/  xUnit tests plus hand-written "Mock*" test doubles that
                           implement the same BlinkCommon interfaces as the real
                           classes, so the real business logic (BlinkLibrary,
                           ApiTransactions) can be tested without any network access.
docs/                      This file and USER_GUIDE.md.
scripts/                   install.ps1 / uninstall.ps1 (see USER_GUIDE.md).
```

Three separate projects exist specifically so the test project can reference
`BlinkCommon`'s interfaces and swap in mocks for `BlinkCameraCall`'s concrete classes,
without the production code ever being compiled with test-only code in it.

## 3. The two architectural patterns everything else is built on

These two choices explain almost every "why is it written like *that*?" moment in
this codebase. If you are rebuilding from scratch and want a simpler codebase, you
could drop both (see §10), but you should understand why they were used first.

### 3.1 "Duck typing" via Shadow.Quack, instead of POCOs/DTOs

Normally a C# API client defines a concrete class per JSON shape (`LoginResponse`,
`AuthAccount`, ...) and deserializes `System.Text.Json` straight into it. This
codebase instead defines only an **interface** per shape (e.g. `ILoginResponse`,
`IAuthAccount` in `BlinkCommon/Interfaces/`), and uses the `Shadow.Quack` library's
`Duck.Implement<T>(...)` to produce a *runtime-generated* object that implements that
interface, backed either by:
- an anonymous object (`Duck.Implement<ILoginResponse>(new { Token = "...", ... })`) -
  used when **constructing** a response in code, e.g. mapping an OAuth result into
  the shape the rest of the app expects, or
- a parsed JSON document (`Duck.Implement<T>((JsonProxy)jsonString)`, wrapped by
  `JsonSerializerExtension.Deserialize<T>`) - used when **parsing** a real HTTP
  response body.

Why: Blink's (undocumented, frequently-changing) API returns deeply nested JSON with
many optional/renamed/removed fields across versions. Hand-written POCOs would need
constant, tedious updates and null-handling. With duck typing, a property that is
missing from the JSON simply reads back as the type's default (`0`, `false`, `null`)
instead of throwing, and adding a field anywhere is a one-line interface change - no
constructor, no `[JsonPropertyName]` attributes, no mapping code. The trade-off is
that typos in property names fail at runtime, not compile time, and the "construction"
call sites (`Duck.Implement<T>(new { ... })`) are stringly-matched against the
interface by reflection, so property names in the anonymous object must exactly match
the interface's property names (case-sensitively) or they are silently dropped.

### 3.2 Service location via Dependency.Shelf, instead of a DI container

There is no `IServiceCollection`/constructor-injection container. Instead,
`Shelf.ShelveInstance<T>(instance)` stores a single instance of `T` in a static,
process-wide slot, and `Shelf.RetrieveInstance<T>()` fetches it back. `BlinkAdapter`
(the real one) and `MockAdapter` (the test one) both call `Shelf.Clear()` then shelve
the same two interfaces:

```csharp
Shelf.ShelveInstance(ApiDriver.HttpClientApiHandler());   // IApiMethods
Shelf.ShelveInstance(ApiDriver.OAuthLoginHandler());      // IOAuthLogin
```

`ApiTransactions` (production business logic) then does
`Shelf.RetrieveInstance<IApiMethods>()` / `Shelf.RetrieveInstance<IOAuthLogin>()`
rather than taking them as constructor parameters. This means swapping real HTTP
calls for test doubles is just "construct a `MockAdapter` instead of a
`BlinkAdapter` before the test" - no DI container wiring, no `IServiceProvider`
plumbing for a console app that only ever has one "scope" (the whole process).

The cost: it's a global mutable singleton, so **tests that read
`Shelf.RetrieveInstance<...>()` without first constructing their own
`MockAdapter`/`BlinkAdapter` in the same test method are reading whatever a
previous, unrelated test happened to shelve last** - this bit us during the OAuth
rewrite (see §9) and is the main reason every test method should start with its own
`new MockAdapter(...)`.

## 4. Call-chain overview

```
Program.cs (REPL loop)
  -> command string  --ProcessCommandString()-->  ICommandAndArguments
  -> BlinkLibrary.Login / Logout / VerifyPin / Get / Help
       -> IAdapter extension methods (AdapterExtension.cs)
            -> IApiTransactions (ApiTransactions, retrieved via Shelf)
                 -> IApiMethods (HttpClientApiHandler)      [plain REST calls]
                 -> IOAuthLogin  (OAuthLoginClient)         [the login flow]
```

- `Program.cs` only knows about `BlinkLibrary`; it never touches HTTP or the Shelf
  directly.
- `BlinkLibrary` holds the only piece of session state in the whole app:
  `ISessionDetails _sessionDetails` (account id, tier, auth token, whether a login
  or a pending two-factor challenge is in progress). It decides *what a command
  means right now* (e.g. whether `VERIFY -v<code>` should complete a two-factor
  login or verify an old-style client-trust PIN - see §6.5).
- `AdapterExtension` is a thin pass-through: it exists so `IAdapter` (otherwise an
  empty marker interface) can be called like `adapter.Login(...)` instead of
  reaching into the Shelf from `BlinkLibrary` directly.
- `ApiTransactions` is where every Blink URL and request shape is known. It is the
  one place that would need to change again if Blink changes an endpoint.

## 5. Settings and persistence (`BlinkCameraCall/Extensions/SettingsFileExtension.cs`)

`config.json` lives next to the executable (`AppContext.BaseDirectory`) and holds:

```json
{ "Email": "...", "Password": "...", "BaseUrl": "https://rest-prod.immedia-semi.com", "HardwareId": "..." }
```

- `Load()` never writes to disk by itself (a unit test explicitly asserts that
  loading from a non-existent file does not create one) - it only fills in defaults
  in memory. `HardwareId` defaults to an empty string if absent.
- `Program.cs` is the one place that *generates and persists* a new `HardwareId`
  (a GUID) the first time it's empty, immediately after `Load()`. See §6.4 for why
  this value exists and why it must survive across runs.
- `Save()` just serializes the whole settings object back to the same file.
- The file is deliberately **not** committed (`.gitignore`) and never faked with
  real credentials anywhere in the repo - tests use `MockSettings.CreateSettings()`
  instead.

## 6. Authentication deep dive

This is the part that broke (Blink retired its old login API in Nov 2025) and the
part most likely to need maintenance again. All of this logic lives in
`BlinkCameraCall/Driver/OAuthLoginClient.cs` and `OAuthConstants.cs`.

### 6.1 Why the old approach stopped working

Until late 2025, logging in was one call: `POST /api/v5/account/login` with
`email`/`password` as a form body, returning a token directly. Blink replaced this
with the same OAuth2 + PKCE "sign in through a hosted web page" flow their iOS app
uses, served from a separate host, `api.oauth.blink.com`. There is no single-call
replacement - logging in now means acting like a browser for several hops.

### 6.2 The flow, step by step, and why each step exists

```
1. GET  /oauth/v2/authorize?...        starts a login session (sets a session
                                        cookie) and registers this device's PKCE
                                        challenge + redirect URI server-side.
2. GET  /oauth/v2/signin               fetches the hosted sign-in page HTML, which
                                        embeds a CSRF token the next POST must echo
                                        back (a normal anti-CSRF pattern for a
                                        form-based login page).
3. POST /oauth/v2/signin               submits { username, password, csrf-token }.
                                          - a redirect (3xx) means login succeeded
                                            outright.
                                          - 202 or 412 means Blink wants a two-factor
                                            code; the response body carries an opaque
                                            "tsv_state" correlating *which* challenge
                                            the eventual code belongs to.
3b. POST /oauth/v2/2fa/verify          (only on the 202/412 path) submits
                                        { 2fa_code, csrf-token, tsv_state,
                                          remember_me: false }. A 2xx HTTP status
                                        does NOT mean the code was accepted - the
                                        body must contain "status":"auth-completed".
                                        A correct response also contains
                                        "redirect_url", almost always the bare path
                                        "/oauth/v2/authorize" (no query string).
4. GET  <redirect_url from step 3/3b>  now that the session cookie is authenticated,
                                        this redirects to the app's custom-scheme
                                        redirect_uri with "?code=..." in the query -
                                        that `code` is the OAuth authorization code.
5. POST /oauth/token                   exchanges { code, code_verifier, hardware_id,
                                        redirect_uri, grant_type: authorization_code,
                                        ... } for { access_token, refresh_token }.
6. GET  /api/v1/users/tier_info        (Authorization: Bearer <access_token>) returns
                                        { account_id, tier }. "tier" selects which
                                        regional host (rest-<tier>.immedia-semi.com)
                                        the rest of the app's REST calls use.
```

Two non-obvious correctness traps found by trial and error against the real API
(both are commented at their fix site in `OAuthLoginClient.cs`, so future-you doesn't
rediscover them the hard way):

- **Step 4 must use the URL Blink just told you to use, not the URL from step 1.**
  Re-sending the original `authorize` request (full PKCE query string and all) after
  signing in is *not* treated as "continue my session" - Blink just serves the
  sign-in page again, as if you were never authenticated. The fix is to always
  follow the real `Location`/`redirect_url` the server just gave you, because the
  pending authorization request is tracked **server-side against the session
  cookie**, not reconstructed from a resent query string.
- **A 2xx from `2fa/verify` is not success.** Blink returns a 2xx HTTP status even
  for a wrong/stale code; the only reliable signal is the JSON body's
  `"status":"auth-completed"`.
- **Redirects can be relative**, and `Uri.Query` throws
  `InvalidOperationException: This operation is not supported for a relative URI`
  on a relative `Uri`. Every `Location` header must be resolved against the request
  URL before anything reads its query string (`ResolveLocation` exists for exactly
  this).
- **The final redirect's scheme isn't http(s).** `redirect_uri` is the custom app
  scheme `immedia-blink://applinks.blink.com/signin/callback`; `HttpClient` cannot
  (and must not try to) fetch it - the code just reads `code` out of its query
  string without sending a request to it. `FindAuthorizationCodeAsync` follows up to
  5 ordinary http(s) redirect hops, stopping as soon as any hop's query string has a
  `code`, or as soon as it hits a non-http(s) scheme.

### 6.3 Two-factor state has to outlive one method call

A console command (`LOGIN`) returns before the user has typed the SMS/email code, so
the in-progress `HttpClient` (which carries the authenticated-pending session cookie),
the PKCE `code_verifier`, the CSRF token, and the `tsv_state` all have to be kept
alive between the `LOGIN` call and the later `VERIFY -v<code>` call.
`OAuthLoginClient` does this by holding a single `PendingTwoFactorSession` record as
instance state, and relying on `BlinkAdapter`/`MockAdapter` to `Shelf.ShelveInstance`
exactly one `IOAuthLogin` instance for the whole process/test (see §3.2) - so the
same object answers both the `Login(...)` and the later `CompleteTwoFactor(...)` call.
If the user runs `LOGIN` again while a challenge is pending, the old session is
disposed and discarded. If `CompleteTwoFactor` fails (wrong code), the session is
deliberately **kept alive** so the user can retry `VERIFY` with a fresh code without
restarting the whole login.

`BlinkLibrary.VerifyPin` is the piece that decides *which* meaning `VERIFY -v<code>`
has: if the last login attempt is not logged in yet but set
`Verification.Phone.Required = true` (the signal `ApiTransactions` maps
`OAuthLoginResult.TwoFactorRequired` onto), the code is routed to
`AuthCompleteTwoFactorLogin`; otherwise it falls through to the old client-trust
`AuthVerifyPin` flow (§6.5).

### 6.4 Why `HardwareId` is persisted in `config.json`

Blink ties a two-factor "trust" decision to a `hardware_id` (a UUID identifying the
"device"). If a fresh random UUID were generated on every login (which is what the
first version of this fix did), Blink would see a "new device" every single run and
demand a two-factor code every single time. `Program.cs` therefore generates the
UUID once, the first time it's missing, and saves it back to `config.json`
immediately - so the same "device" identity is reused on every subsequent run, and
(per Blink's own behaviour) two-factor prompts should become occasional rather than
constant. `OAuthLoginClient.Login` still falls back to generating a one-off random
UUID if it's ever called with an empty `hardwareId`, purely as a safety net (e.g. if
a caller forgets to load settings first) - that fallback intentionally is not
persisted anywhere.

### 6.5 The *other*, older verification flow that still exists

`IApiTransactions.AuthVerifyPin` / `ApiTransactions.AuthVerifyPin` is a **separate,
older** mechanism: the legacy `POST /api/v4/account/{account_id}/client/{client_id}/pin/verify`
call used to mark a *client/device* as trusted on an account that already has a
token (distinct from the OAuth login's own two-factor step in §6.3). It is kept
as-is; nothing in this rewrite changed it. `Client_Id` is a concept that does not
exist in the new OAuth flow's responses at all (`OAuthLoginResult.ClientId` is
always `0` from the real client - see §9 for why it still exists as a field).

### 6.6 Automatic login at startup (`Program.AttemptAutoLogin`)

`Program.cs`'s `Main` calls `AttemptAutoLogin` once, before entering the
command loop, to satisfy "don't make me retype my credentials every run"
without changing anything about how `LOGIN`/`VERIFY` work when typed
manually — it's pure orchestration on top of the existing primitives:

- Skips entirely if `settings.Email`/`Password` are empty, or still the
  literal shipped placeholders (`test@email.com`/`PAssword` —
  `SettingsFileExtension.Load`'s defaults) — a fresh install falls straight
  through to the normal prompt, exactly as before this feature existed.
- Otherwise calls the existing `BlinkLibrary.Login()` (the no-args overload,
  which already reads `IBlinkSettings.Email`/`Password` — functionally the
  same as typing `LOGIN` with no arguments).
- Two new `BlinkLibrary` properties decide what happens next without any
  string-matching on the returned message: `IsLoggedIn` (`_sessionDetails.LoggedInStatus`)
  and `TwoFactorPending` (`!IsLoggedIn && _sessionDetails.Verification?.Phone?.Required == true`).
  If `TwoFactorPending`, it prompts for just the code (not `VERIFY -v<code>` —
  the prefix is added for you) up to 3 times, calling the existing
  `BlinkLibrary.VerifyPin` each time; a wrong code doesn't start over, the
  same pending-challenge behavior §6.3 already documents applies.
- `SaveCredentialsIfLoggedIn` runs after this *and* after every manual
  `LOGIN`/`VERIFY` in the command loop: if `IsLoggedIn`, it reads
  `BlinkLibrary.CurrentSession.Auth.UserName`/`.Password` — already
  populated correctly for both a direct login and a 2FA-completed one,
  since `ApiTransactions` carries the original username/password through
  `_pendingUsername`/`_pendingPassword` (§6.3) into `MapLoginResult`'s
  `Auth` object regardless of which path completed it — and writes them
  into `config.json`. This is how a first manual `LOGIN -u<email> -p<password>`
  (optionally completed with `VERIFY -v<code>`) becomes tomorrow's automatic
  login: nothing has to explicitly "remember" which flow was used.

## 7. Session data and the "home screen"

`SystemGetHomeScreen` (`ApiTransactions`) is an ordinary Bearer-token REST call to
`GET https://rest-<tier>.immedia-semi.com/api/v3/accounts/<account_id>/homescreen`,
untouched by the OAuth rewrite beyond the header change in §8. Its response is
deserialized straight into `IGetHomeScreenResponse` (and its many nested
`IHomeScreen*` interfaces) via the duck-typing JSON path (§3.1), then copied onto
`BlinkLibrary`'s `_sessionDetails` by `BlinkLibrary.Get`.

### 7.1 The `MAP` command

`GET -ihome` fetches this data but only ever prints `"Command ran successfully."` -
none of it is shown anywhere. `MAP` (`BlinkLibrary.Map`) calls the exact same
`adapter.GetHomeScreen(account)` and updates session state the same way, then pipes
the response through `IGetHomeScreenResponse.ToDeviceMapString()`
(`BlinkCameraCall/Extensions/DeviceMapExtension.cs`) to print a Network → Sync
Module → Camera/Doorbell tree.

That extension method is deliberately a pure function - no I/O, no Shelf/adapter
access, just a response object in and a formatted string out - so its grouping and
edge-case logic (a network with no cameras/doorbells, a device whose `Network_Id`
matches no network, null `Signals`) is unit-tested directly against hand-built duck
objects, with no mocking required. The same null-defaulting discipline as elsewhere
in this codebase applies: every one of `Networks`/`Sync_Modules`/`Cameras`/
`Doorbells`/`Sirens`/`Chimes`/`Owls`/`Doorbell_Buttons` is treated as possibly null
regardless of the interface's declared type, because duck-typed objects return null
for anything the caller's anonymous object or the real JSON didn't set.

Two things worth knowing if you touch this again:
- `IHomeScreenDoorbells` originally had **no `Network_Id` property at all** - a
  mock fixture field named `Network_id` (lowercase i) silently never reached it.
  Grouping doorbells by network required adding that property to the interface
  first.
- Blink Mini cameras come back in the home-screen response as `Owls`, a bare
  `string[]` that nothing in this codebase models in detail (see §10). `MAP` shows
  them only as a count (`"Owls (Blink Mini cameras): N (not detailed)"`) rather than
  listing each one - deliberately, to avoid implying a Mini-only account has "no
  cameras" while still not overstating what's actually modeled.

### 7.2 The `DEVICE` command

`BlinkLibrary.Device` does not call Blink at all — it looks up a camera or
doorbell by name (`DeviceLookupExtension.FindCameraByName`/
`FindDoorbellByName`, case-insensitive, `null` on zero or 2+ matches) in
whatever `_sessionDetails` already holds from the last `MAP`/`GET`, resolves
its network from `_sessionDetails.Networks`, and formats both with
`DeviceDetailExtension.ToDeviceDetailString` — a pure function in the same
style as `DeviceMapExtension.ToDeviceMapString` (§7.1).

### 7.3 The `SNAP` command

Triggering a fresh snapshot is Blink's standard async-command pattern, used
for every "do a thing on the physical device" operation in this API:

1. `POST` with no body to a camera- or doorbell-specific `.../thumbnail` path
   (`ApiTransactions.SystemTriggerCameraSnapshot`/`SystemTriggerDoorbellSnapshot`).
2. The response carries a command id; poll `GET /network/{id}/command/{id}`
   once a second for up to 120 attempts until Blink reports `complete: true`
   (`BlinkCameraCall/Driver/CommandPolling.cs`, `WaitForCommandComplete`).
3. On success, `BlinkLibrary.Snap` re-fetches the home screen so the
   device's cached `Thumbnail` field is current for a following `IMAGE`.

`CommandPolling` takes `IApiMethods` and the poll URL directly rather than
reading from `Shelf` itself, so its unit tests
(`UnitTest_CommandPolling`) run against a hand-rolled fake with an injected
`TimeSpan.Zero` poll interval — the real default is a genuine 1-second
sleep, which would make a timeout test take two minutes if not overridden.
For that reason `Snap` (and every other command that polls — `LocalClips`,
`LocalDownload`) exposes optional `pollInterval`/`maxRetries` parameters
that default to the production values; `Program.cs` never passes them.

**Two failure modes the final review caught and this now handles
explicitly, both found by a Blink-rejection fixture that returns a POST
body with no command id:**
- If the trigger/prepare/manifest-request `POST` itself comes back with no
  usable id (Blink refused outright — busy, offline device, expired token),
  `ApiTransactions` throws `InvalidOperationException("Blink rejected the
  request: <raw body>")` immediately, rather than polling a nonexistent
  command for the full two minutes and reporting a misleading timeout.
  `Program.cs`'s top-level catch surfaces this as `Error: Blink rejected
  the request: ...`.
- `CommandPolling` also checks `ICommandStatusResponse.Status_Code`: a
  non-`908` value while `complete: false` is treated as an immediate
  failure (returns `null`, same as a timeout), not as "still pending" —
  otherwise a command Blink already failed would either burn the full
  retry budget or, worse, eventually be read as success.

### 7.4 The `IMAGE` command

A device's thumbnail reference comes in one of two shapes, mirrored from
blinkpy (the reference OSS client) because this project's own testing never
saw the live response directly:

- A bare integer (the new API) — build a fixed-shape URL from it:
  `/api/v3/media/accounts/{account}/networks/{network}/{camera|doorbell}/{id}/thumbnail/thumbnail.jpg?ts={value}&ext=`.
- Any other string (the old API) — append `.jpg`, unless it already ends
  `&ext=`.

`ThumbnailUrlExtension.BuildThumbnailUrl` is a pure function covering both
cases, resolving the result against the tier base URL via `System.Uri`'s
relative-URI combining (not string concatenation) so a value that's already
absolute is left alone — the same technique as `urljoin` in blinkpy.
`BlinkLibrary.Image` then calls `IApiMethods.GetBytes` (added to that
interface alongside the existing text-returning `Get`) through
`IApiTransactions.DownloadBytes`, and writes the result via
`MediaFileExtension.SaveMediaFile`.

`HttpClientApiHandler.GetBytes` checks `response.IsSuccessStatusCode` and
returns `null` on anything but a 2xx — the final review caught that it
originally didn't, which meant a 404/401 error body got written straight
to disk as a `.jpg`/`.mp4` and reported as `Saved image to ...`. Verifying
this needed `HttpClientApiHandler` to go from `internal` to `public` (no
`InternalsVisibleTo` exists in this repo), since the test uses its existing
`HttpMessageHandler`-injecting constructor with a fake handler rather than
the usual `MockHttpClientApiHandler` substitution.

`SNAP` and `IMAGE` resolve a name to a camera *or* doorbell, never both:
`BlinkLibrary.ResolveCameraOrDoorbell` looks up both lists and treats a name
that matches one of each the same as a name that matches neither (ambiguous
= not found), rather than the camera silently winning. `DEVICE` is
deliberately exempt — its spec explicitly resolves camera-first.

### 7.5 `VIDEOS` and `DOWNLOAD` (cloud clips)

`VIDEOS` calls `GET /api/v1/accounts/{account}/media/changed?since={iso8601}&page=1`
and caches the (non-deleted) result on `BlinkLibrary._lastCloudMedia`;
`DOWNLOAD -i<id>` looks the id up in that cache and downloads
`{tier-base}{item.Media}` via the same `DownloadBytes` path as `IMAGE`.

**This is the one area of this release least likely to be exactly right.**
The `media/changed` response's field names (`ICloudMediaItem`'s `Id`,
`Device_Name`, `Media`, `Deleted`, etc.) are a best-effort reconstruction —
none of the source material read for this feature actually parsed that
response's JSON, only referenced the endpoint and the general shape of
"a list of recent media items." Treat a `VIDEOS`/`DOWNLOAD` failure against
a real account the same way the OAuth rewrite treated login failures: the
error message includes Blink's raw response, which is the fastest way to
find and fix a field-name mismatch.

### 7.6 `LOCALCLIPS` and `LOCALDOWNLOAD` (SD-card clips)

Reading a Sync Module's SD card is a three-step dance, because Blink has to
actually ask the physical Sync Module to read the card over its own
connection before any of this data exists server-side:

1. `LOCALCLIPS -n<syncmodule>` `POST`s a manifest request, gets back a
   request id, then polls `GET .../manifest/request/{request_id}` — not for
   a `complete` flag like `CommandPolling`, but for a `clips` array to
   appear in the response. This has its own tiny poller,
   `BlinkCameraCall/Driver/ManifestPolling.cs`, with the same
   injectable-interval shape as `CommandPolling` for the same testing
   reason. The result (sync module id, manifest id, and the clip list) is
   cached on `BlinkLibrary._lastLocalManifest`.
2. `LOCALDOWNLOAD -i<id> -n<syncmodule>` checks the cached manifest belongs
   to the *named* sync module (not just any previously-cached manifest —
   this is the one input class worth testing deliberately, since switching
   sync modules between `LOCALCLIPS` calls is an easy way to end up
   downloading against a stale manifest by accident) and that the clip id
   was actually in it.
3. The clip's URL is unusual: the **same** URL
   (`IApiTransactions.BuildLocalStorageClipUrl`, a fixed template keyed by
   account/network/sync-module/manifest/clip id) is first `POST`ed (which
   tells the Sync Module to pull that clip off the card into a
   cloud-accessible buffer — polled with `CommandPolling`, same as `SNAP`)
   and then `GET`ed for the actual bytes. There is no separate "download"
   URL.

Deliberately **not** implemented: the delete-from-card step blinkpy's own
local-storage flow has after downloading. Silently deleting footage off
someone's physical SD card by default is a worse failure mode than making
them delete it from the Blink app if they want the space back.

### 7.7 The shell navigation layer (CD/LS/TRIGGER, bare DOWNLOAD)

`CD`/`LS`/`TRIGGER` and a bare `DOWNLOAD <name>` (as opposed to the legacy
`DOWNLOAD -i<id>`) let the REPL feel like a PowerShell session: a prompt
that tracks where you are, and commands that act on "the current device"
instead of repeating its name every time. All of this lives in
`ShellSession` (top-level in `BlinkCameraCall`), which wraps a
`BlinkLibrary` the same way `BlinkLibrary` itself wraps `IAdapter` —
constructor-injected, one instance for the process.

- **Hubs are `Network`s, not `SyncModule`s.** A network's directly-attached
  cameras/doorbells all appear under it in `CD`/`LS` even when some don't
  route through a sync module at all (Blink Minis use none, and stay
  unmodeled here the same way `MAP` leaves them as a bare count — see §10).
  Modeling the tree on `Network` rather than `SyncModule` means `LS` at the
  hub level doesn't have to special-case a network with zero or multiple
  sync modules.
- **Bare-positional parsing lives alongside the existing `" -"`-splitting
  path, not instead of it.** `CommandProcessorExtension.FormatCommand`
  checks `verb` against a small `BarePositionalCommands` set (`cd`, `ls`,
  `trigger`) *before* ever reaching the old `consoleCommand.Split(" -")`
  logic, and `download` gets its own one-off check (non-empty remainder
  that doesn't start with `-`) that sets the new `ICommandAndArguments.
  BareDownload` flag. Every verb that doesn't match either check falls
  through to the untouched legacy path, so none of the pre-existing
  flag-command tests needed to change.
- **`ShellLineEditor`/`IConsoleIO` exist purely as a testability seam.**
  `IConsoleIO` (`ReadKey`, `Write`, `CursorLeft`/`CursorTop`,
  `WindowWidth`) is the only thing `ShellLineEditor` touches; `Program.cs`
  wires it to the real console via `SystemConsoleIO`, while
  `UnitTest_ShellLineEditor` drives it against a hand-rolled fake that
  queues `ConsoleKeyInfo` values and records every `Write`/cursor move —
  the same "construct a fake at the seam" approach `CommandPolling`/
  `ManifestPolling` already use for their own injectable timing (§7.3,
  §7.6), applied here to console I/O instead of HTTP polling.
- **`ShellSession`'s device-media cache is explicitly cleared whenever you
  leave the current device, not detected as stale by comparison.**
  `ChangeDirectory` calls `ClearDeviceState()` directly (which nulls both
  `_lastDeviceMediaListing` and `_lastDeviceMediaListingFor`) only on `cd ..`
  from a device, and calls `ClearAllState()` (which calls `ClearDeviceState()`
  too) on both `cd ..` from a hub and on bare `cd`/`cd \`/`cd /` from any
  level. Because the branch
  that assigns a new `_currentDeviceName` is only reachable when
  `_currentDeviceName` is already `null`, you can never land in a *different*
  device without first passing through one of these clears — so a fresh `LS`
  always replaces the cache, and a following `DownloadByName` for a
  previous device's entry is already impossible by construction, not caught
  after the fact. The `_lastDeviceMediaListingFor != _currentDeviceName`
  check `DownloadByName` still does is a defensive backstop (and what
  actually produces `"Run LS first."` when no `LS` has run yet in the
  current device), not the mechanism doing the day-to-day invalidation
  work. This is a different shape from `LOCALDOWNLOAD`'s guard against a
  stale `LOCALCLIPS` manifest (§7.6), which *is* a real by-comparison
  staleness check, because `BlinkLibrary` has no equivalent "leaving a
  sync module" event to clear on.

### 7.8 The `WATCH`/`UNWATCH` commands (background event polling)

`WATCH -s<seconds>` starts a background poll for new doorbell/motion cloud
events and alerts in the console when one appears; `UNWATCH` stops it.
Unlike every other command in this app, it does not block the REPL loop —
the shell keeps accepting commands normally while it runs.

- **Polling logic lives in `EventWatchPolling.Run`**
  (`BlinkCameraCall/Driver/EventWatchPolling.cs`), which calls the exact
  same `adapter.GetRecentMedia(account, sinceUtc)` as `VIDEOS` (§7.5),
  tracks a `HashSet<long>` of clip ids already reported so the same event
  is never alerted twice, and rolls `sinceUtc` back 30 seconds on every
  poll as a clock-drift buffer (safe only because dedup is by id, not by
  the `since` boundary). It follows the same shape as
  `CommandPolling`/`ManifestPolling` (§7.3, §7.6) specifically so it can be
  unit-tested the same way: a `maxIterations` parameter bounds the loop,
  letting tests call it synchronously with `TimeSpan.Zero` and a small
  iteration count instead of needing a real background thread at all.
- **`BlinkLibrary.Watch`/`Unwatch` own a single `CancellationTokenSource`**
  and wrap `EventWatchPolling.Run` in a real `Task.Run` for production.
  `Watch` refuses a second call while one is already running
  ("Already watching. Use UNWATCH to stop."), and `Logout` calls the same
  stop path `Unwatch` does, so a watch left running doesn't keep polling
  with an invalidated session after logging out.
- **`Program.cs` starts it automatically** after every successful login —
  the startup auto-login (§6.6), a manual `LOGIN`, and a completed `VERIFY`
  2FA challenge all call a small `TryAutoStartWatch` helper once they know
  `blinkLibrary.IsLoggedIn`. You can still stop or restart it manually with
  `UNWATCH`/`WATCH` at any time.
- **The one real complexity: alerting while a line is being typed.**
  `ShellLineEditor` used to track its prompt/buffer/cursor position purely
  as locals inside `ReadLine`, with no concurrency awareness at all — a raw
  `Console.WriteLine` from the watch thread mid-keystroke would print into
  the middle of your typed command and corrupt the editor's cursor math.
  Those locals are now instance fields guarded by a `lock`, and a new
  `NotifyExternal(message)` method takes that same lock to erase the
  in-progress prompt+buffer, print the alert, and redraw both — restoring
  exactly where your cursor was. `ReadLine`'s own key-handling takes the
  same lock, but deliberately *not* around the blocking `io.ReadKey()` call
  itself, so an alert can appear immediately instead of waiting for your
  next keystroke. `Program.cs` picks `NotifyExternal` as the alert sink in
  interactive mode, or a plain `Console.WriteLine` when input/output is
  redirected (no line editor is in play there). `NotifyExternal` assumes
  the prompt always starts at column 0, which holds for every caller in
  this app (each `ReadLine` follows a newline) — it doesn't need to track
  an arbitrary start column to stay correct.
- **Alert format and sound**: `[ALERT] New event: <Device_Name> at
  <Created_At> UTC (id <Id>)`, plus `Console.Beep()` from the interactive
  alert sink (skipped in redirected mode — no one to hear it, and some
  environments don't support it). A poll that throws calls the same sink
  with a `[WATCH] Error checking for new events: ...` message instead of
  killing the background loop — a transient API hiccup shouldn't require
  restarting the watch.
- **Testing the concurrency**: proving `NotifyExternal` doesn't corrupt an
  in-progress line needs a `ReadLine` call that's genuinely paused
  mid-keystroke on another thread, which the existing `FakeConsoleIO`
  (queue-and-throw-when-empty) can't do. `BlockingFakeConsoleIO`
  (`BlinkCameraCallUnitTests/IO/BlockingFakeConsoleIO.cs`) instead blocks
  `ReadKey()` on a real queue and exposes `WaitForReadKeyCalls(count,
  timeout)`, which blocks the test thread until the editor has processed
  that many keys and is idle waiting for the next one — the safe,
  deterministic window to call `NotifyExternal` without a sleep-based
  guess.

## 8. How an access token is actually used after login

`HttpClientApiHandler.SetAccessToken` sets a `DefaultRequestHeaders` entry once per
login and every later `Post`/`Get`/`Put` call on that same `HttpClient` reuses it.
Before this rewrite it set a bespoke `TOKEN_AUTH: <token>` header (Blink's old
convention); the new API expects the standard `Authorization: Bearer <token>`
header instead, which is what the current code sends.

## 9. Testing strategy

- `BlinkCameraCallUnitTests` never hits the network. Every test constructs either a
  `BlinkAdapter` (only in the one test that specifically checks the real wiring,
  `UnitTest_AdapterExtension`) or, in every other test, a `MockAdapter`, which
  shelves `MockHttpClientApiHandler : IApiMethods` and
  `MockOAuthLoginClient : IOAuthLogin` instead of the real network-calling classes.
- `MockOAuthLoginClient` recognises three fixed accounts from `MockSettings`
  (a normal login, a deliberately-wrong login, and one that always requires
  two-factor with the fixed code `987654`), letting
  `UnitTest_BlinkLibrary.Test_LoginRequiresTwoFactor_*` exercise the full
  pending-session/`VERIFY` routing in §6.3 without any HTTP involved.
- **Gotcha this rewrite ran into**: several pre-existing tests read
  `Shelf.RetrieveInstance<IApiTransactions>()` via a `private static` property
  *without* constructing their own `MockAdapter` first, silently depending on
  whichever earlier test in the process happened to shelve one last. That's
  normally invisible, but once `AuthLogin` started making a real OAuth HTTP call
  when nothing had been shelved, one such test actually hit Blink's live server.
  The fix was mechanical but important: **every test method must construct its own
  `MockAdapter` (or `BlinkAdapter`) before reading from the Shelf**, never rely on
  another test having done it first.
- `OAuthLoginResult.ClientId` exists purely so `MockData.OAuthLoginCorrectResult()`
  can still hand back the `1486061` client id the pre-existing logout/pin-verify
  tests expect, even though the real `OAuthLoginClient` never populates it (that
  concept doesn't exist in the new API - see §6.5). This keeps unrelated tests
  passing without pretending the production code knows something it doesn't.

## 10. Known limitations / honest gaps

If you are rebuilding this from scratch, these are the things to either fix properly
or deliberately decide to still skip:

- **Logout and client-trust pin-verify still assume `Client_Id` exists.** The new
  OAuth flow never returns one. These two commands are untested against the real,
  current Blink API and may need their own rewrite the same way login did.
- **No refresh-token use.** `OAuthLoginResult.RefreshToken` is captured but nothing
  ever calls Blink's refresh endpoint with it; every `LOGIN` does the full flow from
  scratch. A long-running process could use the refresh token to avoid repeating
  steps 1-4.
- **CSRF/`tsv_state`/`redirect_url` extraction is regex-over-raw-text, not a real
  HTML/JSON parser**, because the sign-in page is one big inlined `<script>` blob
  rather than clean JSON. This is brittle by nature - if Blink changes their sign-in
  page's markup, `ExtractCsrfToken`/`ExtractTsvState`/`ExtractRedirectUrl` are the
  first things to re-check, and each failure path already reports Blink's raw
  response body for exactly this reason.
- **`config.json` stores the account password and device id in plain text** next
  to the executable. Acceptable for a personal single-user console tool; not
  acceptable to redistribute as-is.
- **The `CommandProcessorExtension` class (in `BlinkCameraCall/Extensions/`) is
  declared in the `BlinkCameraCallUnitTests.Extensions` namespace.** This is a
  pre-existing oddity (not introduced by the login rewrite) that `Program.cs`
  depends on via `using BlinkCameraCallUnitTests.Extensions;` - harmless, but
  confusing, and worth fixing if you're already rebuilding from scratch.
- **`Sirens`/`Chimes`/`Owls`/`Doorbell_Buttons` on `IGetHomeScreenResponse` are
  still raw `string[]` placeholders**, not modeled with real device shapes the way
  `Cameras`/`Doorbells`/`Sync_Modules` are. `MAP` (§7.1) shows them only as a bare
  count rather than pretending to detail them. Blink Mini cameras (`Owls`) are the
  one case worth modeling properly first, since they're actual cameras a user would
  expect to see individually, not an accessory. This also means `DEVICE`, `SNAP`,
  `IMAGE`, `LOCALCLIPS` and `LOCALDOWNLOAD` (§7.2-7.6) can't address them either —
  they only look up cameras, doorbells, and sync modules.
- **No liveview (live video streaming).** Blink's liveview endpoint returns an
  `immis://`-style stream URL, not a downloadable file; a console app has nothing
  to point that at. `SNAP`/`IMAGE` give you a still image instead.
- **No delete-from-SD-card step after `LOCALDOWNLOAD`** (§7.6) — a deliberate
  omission, not an oversight. blinkpy's own local-storage flow has an optional
  delete-after-download step; this project leaves clips on the card.
- **The `media/changed` response shape behind `VIDEOS`/`DOWNLOAD` (§7.5) is a
  reconstruction, not a confirmed live response.** Expect it to need the same
  live-error-driven field-name fix the OAuth rewrite needed for login.
- **No persisted command history across runs.** `ShellLineEditor`'s Up/Down
  recall (§7.7) is in-memory for the session only, matching how
  `_sessionDetails`/caches already reset on restart.
- **No tab-completion.** `ShellLineEditor` only supports the editing/
  navigation keys it was built with (Left/Right/Home/End/Delete/Backspace,
  Up/Down for history); nothing here rules tab-completion out later, it
  just wasn't asked for.
- **A device-directory `LS` can be slow.** Building the merged media
  listing polls the parent sync module's manifest exactly like
  `LOCALCLIPS` does (§7.6) — up to 120×1s — whenever that device has an
  active local-storage sync module.
- **The line editor's redraw assumes a stable `Console.WindowWidth` for the
  duration of one keystroke.** A terminal resize mid-line may misplace the
  cursor until the next full prompt redraw — not solved here, and
  PowerShell itself isn't perfectly robust to this either.
- **`WATCH` is polling, not push.** It calls the same `media/changed`
  endpoint `VIDEOS` uses on a timer (default 20s) rather than receiving a
  live push from Blink, so there's up to one poll interval of latency
  before a real-world event shows up as a console alert. It also inherits
  §7.5's caveat that the response shape is a best-effort reconstruction,
  not confirmed live.
- **Watch alerts aren't logged anywhere** — they only print to the console
  (and beep) for as long as the process is running. `docs/ToDo.md`'s
  "log them to a file" idea is still open.

## 11. If you had to rebuild this from scratch, in order

1. Define the interfaces in `BlinkCommon` first (`IBlinkSettings`, `ISessionDetails`,
   `ICommandAndArguments`, the `Auth`/`System` response shapes) - they are the
   contract everything else is written against, and nothing else needs a concrete
   implementation yet.
2. Pick your duck-typing/JSON story (§3.1) or decide to use plain POCOs instead;
   either works, but it changes how every "construct a response" call site reads.
3. Write `OAuthLoginClient` against §6.2 directly - it is the one component that
   must match Blink's live behaviour, so get it working against a real account
   (including the two-factor branch) before anything else.
4. Build `ApiTransactions`/`IApiTransactions` as the single seam between "business
   logic" and "HTTP", then `BlinkLibrary` on top of it for command-level behaviour
   (login/logout/verify/get), then the thin `Program.cs` REPL last.
5. Write the `Mock*` test doubles alongside the real classes from the start (not
   after), and get into the habit from day one of constructing a fresh
   `MockAdapter` in every single test method (§9) - the Shelf pattern makes it very
   easy to accidentally write a test that only passes because of execution order.
