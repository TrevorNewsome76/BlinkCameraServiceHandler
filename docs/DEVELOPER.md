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

`settings.json` lives next to the executable (`AppContext.BaseDirectory`) and holds:

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

### 6.4 Why `HardwareId` is persisted in `settings.json`

Blink ties a two-factor "trust" decision to a `hardware_id` (a UUID identifying the
"device"). If a fresh random UUID were generated on every login (which is what the
first version of this fix did), Blink would see a "new device" every single run and
demand a two-factor code every single time. `Program.cs` therefore generates the
UUID once, the first time it's missing, and saves it back to `settings.json`
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

## 7. Session data and the "home screen"

`SystemGetHomeScreen` (`ApiTransactions`) is an ordinary Bearer-token REST call to
`GET https://rest-<tier>.immedia-semi.com/api/v3/accounts/<account_id>/homescreen`,
untouched by the OAuth rewrite beyond the header change in §8. Its response is
deserialized straight into `IGetHomeScreenResponse` (and its many nested
`IHomeScreen*` interfaces) via the duck-typing JSON path (§3.1), then copied onto
`BlinkLibrary`'s `_sessionDetails` by `BlinkLibrary.Get`.

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
- **`settings.json` stores the account password and device id in plain text** next
  to the executable. Acceptable for a personal single-user console tool; not
  acceptable to redistribute as-is.
- **The `CommandProcessorExtension` class (in `BlinkCameraCall/Extensions/`) is
  declared in the `BlinkCameraCallUnitTests.Extensions` namespace.** This is a
  pre-existing oddity (not introduced by the login rewrite) that `Program.cs`
  depends on via `using BlinkCameraCallUnitTests.Extensions;` - harmless, but
  confusing, and worth fixing if you're already rebuilding from scratch.

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
