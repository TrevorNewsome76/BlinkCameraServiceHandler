using BlinkCommon.Interfaces.Auth;

namespace BlinkCameraCallUnitTests;

internal class MockOAuthLoginClient : IOAuthLogin
{
    private bool _twoFactorPending;

    public OAuthLoginResult Login(string email, string password, string hardwareId)
    {
        if (email == MockSettings.CreateTwoFactorSettings().Email && password == MockSettings.CreateTwoFactorSettings().Password)
        {
            _twoFactorPending = true;
            return MockData.OAuthLoginTwoFactorRequiredResult();
        }

        if (email == MockSettings.CreateSettings().Email && password == MockSettings.CreateSettings().Password)
            return MockData.OAuthLoginCorrectResult();

        if (email == MockSettings.CreateNoThumbnailSettings().Email && password == MockSettings.CreateNoThumbnailSettings().Password)
            return MockData.OAuthLoginNoThumbnailResult();

        if (email == MockSettings.CreateNoActiveLocalStorageSettings().Email && password == MockSettings.CreateNoActiveLocalStorageSettings().Password)
            return MockData.OAuthLoginNoActiveLocalStorageResult();

        return MockData.OAuthLoginFailedResult();
    }

    public OAuthLoginResult CompleteTwoFactor(string code)
    {
        if (!_twoFactorPending)
            return MockData.OAuthLoginFailedResult();

        if (code != "987654")
            return MockData.OAuthLoginFailedResult();

        _twoFactorPending = false;
        return MockData.OAuthLoginCorrectResult();
    }
}
