using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlinkCommon.Interfaces.Auth;

namespace BlinkCameraCall.Driver;

/// <summary>
/// Logs into Blink using the OAuth2/PKCE flow Blink switched to in late 2025, in place of
/// the old POST /api/v5/account/login. The flow walks through Blink's hosted sign-in pages
/// exactly as the official iOS app does:
///   1. GET  /oauth/v2/authorize    (starts a login session, PKCE challenge)
///   2. GET  /oauth/v2/signin       (fetch the sign-in page, scrape its CSRF token)
///   3. POST /oauth/v2/signin       (submit credentials; a 202/412 means 2FA is required)
///   3b. POST /oauth/v2/2fa/verify  (only if step 3 required it; see CompleteTwoFactor)
///   4. GET  /oauth/v2/authorize    (now authenticated; the redirect Location carries the auth code)
///   5. POST /oauth/token           (exchange the auth code for an access/refresh token)
///   6. GET  /api/v1/users/tier_info (resolve the account id and regional tier)
/// If step 3 comes back as a two-factor challenge, the in-progress HttpClient/session is kept
/// alive on this instance until CompleteTwoFactor() is called, so the same instance must be
/// reused for both calls (BlinkAdapter/MockAdapter shelve a single instance for this reason).
/// </summary>
internal sealed class OAuthLoginClient : IOAuthLogin
{
    private readonly HttpMessageHandler? _injectedHandler;

    private PendingTwoFactorSession? _pendingSession;

    public OAuthLoginClient()
    {
    }

    public OAuthLoginClient(HttpMessageHandler messageHandler)
    {
        _injectedHandler = messageHandler;
    }

    public OAuthLoginResult Login(string email, string password, string hardwareId) =>
        LoginAsync(email, password, hardwareId).GetAwaiter().GetResult();

    public OAuthLoginResult CompleteTwoFactor(string code) =>
        CompleteTwoFactorAsync(code).GetAwaiter().GetResult();

    private async Task<OAuthLoginResult> LoginAsync(string email, string password, string hardwareId)
    {
        _pendingSession?.Client.Dispose();
        _pendingSession = null;

        if (string.IsNullOrEmpty(hardwareId))
            hardwareId = Guid.NewGuid().ToString().ToUpperInvariant();

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var authorizeUrl = $"{OAuthConstants.AuthorizeEndpoint}?{BuildAuthorizeQuery(codeChallenge, hardwareId)}";

        var handler = _injectedHandler ?? new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler, disposeHandler: _injectedHandler is null)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(OAuthConstants.UserAgent);

        var ownsClient = true;
        try
        {
            await GetFollowingRedirectsAsync(client, authorizeUrl);
            var signinPage = await GetFollowingRedirectsAsync(client, OAuthConstants.SigninEndpoint);

            var csrfToken = ExtractCsrfToken(signinPage);
            if (csrfToken is null)
                return Failed("Could not find a CSRF token on Blink's sign-in page; its markup may have changed.");

            using var signinResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, OAuthConstants.SigninEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = email,
                    ["password"] = password,
                    ["csrf-token"] = csrfToken,
                }),
            });

            if (signinResponse.StatusCode is HttpStatusCode.Accepted or (HttpStatusCode)412)
            {
                var challengeBody = await signinResponse.Content.ReadAsStringAsync();
                var tsvState = ExtractTsvState(challengeBody);

                _pendingSession = new PendingTwoFactorSession(client, authorizeUrl, codeVerifier, hardwareId, csrfToken, tsvState);
                ownsClient = false;

                return new OAuthLoginResult
                {
                    Success = false,
                    TwoFactorRequired = true,
                    Message = "Blink sent a two-factor verification code to complete login; " +
                               "use VERIFY -v<code> to enter it.",
                };
            }

            if (!IsRedirect(signinResponse.StatusCode) || signinResponse.Headers.Location is null)
            {
                var body = await signinResponse.Content.ReadAsStringAsync();
                return Failed($"Blink rejected the sign-in request ({(int)signinResponse.StatusCode}): {body}");
            }

            // Follow sign-in's own redirect rather than re-issuing the original authorize
            // request: re-sending its query string isn't recognised as a session continuation.
            var nextUrl = ResolveLocation(OAuthConstants.SigninEndpoint, signinResponse.Headers.Location).ToString();
            return await CompleteAfterSigninAsync(client, nextUrl, codeVerifier, hardwareId);
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    private async Task<OAuthLoginResult> CompleteTwoFactorAsync(string code)
    {
        var session = _pendingSession;
        if (session is null)
            return Failed("No login is waiting for a two-factor code; run LOGIN again first.");

        var verifyForm = new Dictionary<string, string>
        {
            ["2fa_code"] = code,
            ["csrf-token"] = session.CsrfToken,
            ["remember_me"] = "false",
        };
        if (!string.IsNullOrEmpty(session.TsvState))
            verifyForm["tsv_state"] = session.TsvState;

        using var verifyResponse = await session.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, OAuthConstants.TwoFaVerifyEndpoint)
        {
            Content = new FormUrlEncodedContent(verifyForm),
        });

        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();

        if (!verifyResponse.IsSuccessStatusCode || !IndicatesTwoFactorSuccess(verifyBody))
            // Leave _pendingSession in place so a mistyped code can be retried with VERIFY again.
            return Failed($"Blink did not confirm the verification code ({(int)verifyResponse.StatusCode}): {Truncate(verifyBody)}");

        _pendingSession = null;

        // Blink tells us explicitly where to continue (typically the bare path
        // "/oauth/v2/authorize" with no query string); fall back to the original
        // authorize URL only if that field is missing.
        var redirectUrl = ExtractRedirectUrl(verifyBody) is { } relative
            ? ResolveLocation(OAuthConstants.OAuthBaseUrl, new Uri(relative, UriKind.RelativeOrAbsolute)).ToString()
            : session.AuthorizeUrl;

        try
        {
            var result = await CompleteAfterSigninAsync(session.Client, redirectUrl, session.CodeVerifier, session.HardwareId);

            // Surface what the verify step actually returned alongside any later failure,
            // since a 2xx "success" there does not guarantee the session ends up authenticated.
            if (!result.Success)
                result = new OAuthLoginResult
                {
                    Success = false,
                    TwoFactorRequired = result.TwoFactorRequired,
                    Message = $"{result.Message} [2fa/verify responded {(int)verifyResponse.StatusCode}: {Truncate(verifyBody)}]",
                };

            return result;
        }
        finally
        {
            session.Client.Dispose();
        }
    }

    private static async Task<OAuthLoginResult> CompleteAfterSigninAsync(
        HttpClient client, string authorizeUrl, string codeVerifier, string hardwareId)
    {
        var (authorizationCode, failureDetail) = await FindAuthorizationCodeAsync(client, authorizeUrl);
        if (authorizationCode is null)
            return Failed($"Blink did not return an authorization code after sign-in. {failureDetail}");

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, OAuthConstants.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["app_brand"] = OAuthConstants.AppBrand,
                ["client_id"] = OAuthConstants.ClientId,
                ["code"] = authorizationCode,
                ["code_verifier"] = codeVerifier,
                ["grant_type"] = "authorization_code",
                ["hardware_id"] = hardwareId,
                ["redirect_uri"] = OAuthConstants.RedirectUri,
                ["scope"] = OAuthConstants.Scope,
            }),
        };
        tokenRequest.Headers.UserAgent.Clear();
        tokenRequest.Headers.UserAgent.ParseAdd(OAuthConstants.TokenUserAgent);

        using var tokenResponse = await client.SendAsync(tokenRequest);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            return Failed($"Blink's token exchange failed ({(int)tokenResponse.StatusCode}): {tokenBody}");

        string accessToken;
        string refreshToken;
        try
        {
            using var tokenJson = JsonDocument.Parse(tokenBody);
            accessToken = tokenJson.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            refreshToken = tokenJson.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return Failed($"Could not parse Blink's token response: {tokenBody}");
        }

        if (string.IsNullOrEmpty(accessToken))
            return Failed($"Blink's token response did not contain an access token: {tokenBody}");

        using var tierRequest = new HttpRequestMessage(HttpMethod.Get, OAuthConstants.TierInfoEndpoint);
        tierRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var tierResponse = await client.SendAsync(tierRequest);
        var tierBody = await tierResponse.Content.ReadAsStringAsync();

        if (!tierResponse.IsSuccessStatusCode)
            return Failed($"Logged in, but could not retrieve account information ({(int)tierResponse.StatusCode}): {tierBody}");

        try
        {
            using var tierJson = JsonDocument.Parse(tierBody);
            var tier = tierJson.RootElement.TryGetProperty("tier", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var accountId = tierJson.RootElement.TryGetProperty("account_id", out var aid) ? aid.GetInt64() : 0L;

            return new OAuthLoginResult
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Tier = tier,
                AccountId = accountId,
                Message = "Login successful",
            };
        }
        catch (JsonException)
        {
            return Failed($"Could not parse Blink's account tier response: {tierBody}");
        }
    }

    /// <summary>
    /// Follows the authorize endpoint's redirect chain looking for a "code" query parameter.
    /// Blink may bounce through an intermediate redirect before reaching the final
    /// (non-http) redirect_uri that actually carries the code, so each hop is inspected
    /// rather than assuming the first Location header is the final one.
    /// </summary>
    private static async Task<(string? Code, string Detail)> FindAuthorizationCodeAsync(
        HttpClient client, string startUrl, int maxHops = 5)
    {
        var currentUrl = startUrl;

        for (var i = 0; i < maxHops; i++)
        {
            using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, currentUrl));

            if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
            {
                var body = await response.Content.ReadAsStringAsync();
                return (null, $"Got ({(int)response.StatusCode}) with no redirect instead: {Truncate(body)}");
            }

            var location = ResolveLocation(currentUrl, response.Headers.Location);
            var code = ExtractQueryParameter(location, "code");
            if (code is not null)
                return (code, string.Empty);

            if (location.Scheme is not ("http" or "https"))
                return (null, $"Final redirect ({(int)response.StatusCode}) had no code: {location}");

            currentUrl = location.ToString();
        }

        return (null, $"Followed {maxHops} redirects without finding a code.");
    }

    /// <summary>
    /// Blink's 2fa/verify can return a 2xx status even when the code was wrong, so the
    /// body's own status field has to be checked rather than trusting the HTTP status alone.
    /// </summary>
    private static bool IndicatesTwoFactorSuccess(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("status", out var status) &&
                   string.Equals(status.GetString(), "auth-completed", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Truncate(string value, int maxLength = 300) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static OAuthLoginResult Failed(string message) => new() { Success = false, Message = message };

    private static async Task<string> GetFollowingRedirectsAsync(HttpClient client, string url, int maxRedirects = 5)
    {
        var currentUrl = url;

        for (var i = 0; i < maxRedirects; i++)
        {
            using var response = await client.GetAsync(currentUrl);

            if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
                return await response.Content.ReadAsStringAsync();

            currentUrl = ResolveLocation(currentUrl, response.Headers.Location).ToString();
        }

        throw new InvalidOperationException("Too many redirects while contacting Blink's sign-in page.");
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and < 400;

    /// <summary>
    /// Blink's redirects are sometimes relative; a relative Uri throws on any access to
    /// .Query, so every redirect Location must be resolved against the request URL first.
    /// </summary>
    private static Uri ResolveLocation(string requestUrl, Uri location) =>
        location.IsAbsoluteUri ? location : new Uri(new Uri(requestUrl), location);

    private static string? ExtractCsrfToken(string html)
    {
        var match = Regex.Match(html, "\"csrf[-_]?token\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The 202/412 two-factor challenge response carries an opaque "tsv_state" identifying
    /// which challenge the user's code belongs to; echoed back on 2fa/verify when present.
    /// </summary>
    private static string? ExtractTsvState(string body)
    {
        var match = Regex.Match(body, "\"tsv_state\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// A successful 2fa/verify response names where to continue (e.g. "redirect_url":
    /// "/oauth/v2/authorize", with no query string - the pending authorize request is
    /// tracked server-side against the session cookie, not re-sent by the client).
    /// </summary>
    private static string? ExtractRedirectUrl(string body)
    {
        var match = Regex.Match(body, "\"redirect_url\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractQueryParameter(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == name)
                return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }

    private static string BuildAuthorizeQuery(string codeChallenge, string hardwareId)
    {
        var parameters = new Dictionary<string, string>
        {
            ["app_brand"] = OAuthConstants.AppBrand,
            ["app_version"] = OAuthConstants.AppVersion,
            ["client_id"] = OAuthConstants.ClientId,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["device_brand"] = OAuthConstants.DeviceBrand,
            ["device_model"] = OAuthConstants.DeviceModel,
            ["device_os_version"] = OAuthConstants.DeviceOsVersion,
            ["hardware_id"] = hardwareId,
            ["redirect_uri"] = OAuthConstants.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = OAuthConstants.Scope,
        };

        return string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    private static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string GenerateCodeChallenge(string codeVerifier) =>
        Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record PendingTwoFactorSession(
        HttpClient Client, string AuthorizeUrl, string CodeVerifier, string HardwareId, string CsrfToken, string? TsvState);
}
