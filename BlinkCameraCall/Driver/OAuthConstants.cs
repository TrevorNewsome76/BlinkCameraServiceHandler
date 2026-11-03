namespace BlinkCameraCall.Driver;

/// <summary>
/// Endpoints and client identity for Blink's OAuth2/PKCE login flow, which replaced the
/// old single-call POST to /api/v5/account/login around November 2025 ("ios app flow").
/// Modelled on the open-source blinkpy client (github.com/fronzbot/blinkpy).
/// </summary>
internal static class OAuthConstants
{
    public const string OAuthBaseUrl = "https://api.oauth.blink.com";

    public const string AuthorizeEndpoint = OAuthBaseUrl + "/oauth/v2/authorize";
    public const string SigninEndpoint = OAuthBaseUrl + "/oauth/v2/signin";
    public const string TwoFaVerifyEndpoint = OAuthBaseUrl + "/oauth/v2/2fa/verify";
    public const string TokenEndpoint = OAuthBaseUrl + "/oauth/token";
    public const string TierInfoEndpoint = "https://rest-prod.immedia-semi.com/api/v1/users/tier_info";

    public const string ClientId = "ios";
    public const string Scope = "client";
    public const string AppBrand = "blink";
    public const string AppVersion = "50.1";
    public const string DeviceBrand = "Apple";
    public const string DeviceModel = "iPhone16,1";
    public const string DeviceOsVersion = "18.7";
    public const string RedirectUri = "immedia-blink://applinks.blink.com/signin/callback";

    public const string UserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_7 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.1 Mobile/15E148 Safari/604.1";

    public const string TokenUserAgent = "Blink/2511191620 CFNetwork/3860.200.71 Darwin/25.1.0";
}
