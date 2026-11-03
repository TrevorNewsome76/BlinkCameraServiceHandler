namespace BlinkCommon.Interfaces.Auth;

/// <summary>
/// Performs Blink's OAuth2/PKCE login flow against api.oauth.blink.com.
/// </summary>
public interface IOAuthLogin
{
    /// <param name="hardwareId">
    /// A persisted UUID identifying this device to Blink. Reusing the same value across
    /// logins lets Blink recognise the device, avoiding a repeated two-factor challenge.
    /// If empty, a one-off random id is generated for this login only.
    /// </param>
    OAuthLoginResult Login(string email, string password, string hardwareId);

    /// <summary>
    /// Completes a login that returned <see cref="OAuthLoginResult.TwoFactorRequired"/>,
    /// using the verification code Blink sent to the account's phone/email.
    /// Must be called on the same instance that returned the pending challenge.
    /// </summary>
    OAuthLoginResult CompleteTwoFactor(string code);
}
