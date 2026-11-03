namespace BlinkCommon.Interfaces.Auth;

/// <summary>
/// Result of Blink's current OAuth2/PKCE login flow (introduced ~November 2025,
/// replacing the old single-call email/password POST to /api/v5/account/login).
/// </summary>
public class OAuthLoginResult
{
    public bool Success { get; init; }

    /// <summary>
    /// True when Blink responded with a two-factor (TSV) challenge instead of completing login.
    /// This application does not yet complete the 2FA step.
    /// </summary>
    public bool TwoFactorRequired { get; init; }

    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string Tier { get; init; } = string.Empty;
    public long AccountId { get; init; }

    /// <summary>
    /// Not returned by the new OAuth flow; retained only so that other commands
    /// (logout, pin verification) built against the old account/client id URLs
    /// keep working when this field is populated by a caller that still has one.
    /// </summary>
    public long ClientId { get; init; }

    public string Message { get; init; } = string.Empty;
}
