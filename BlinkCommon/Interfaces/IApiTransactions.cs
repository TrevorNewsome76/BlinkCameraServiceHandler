using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;

namespace BlinkCommon.Interfaces;

public interface IApiTransactions
{
    bool SetAccessToken(string accessToken);
    ILoginResponse? AuthLogin();
    ILoginResponse? AuthLogin(string username, string password);
    ILoginResponse? AuthCompleteTwoFactorLogin(string code);
    ILogoutResponse? AuthLogout(IAuthAccount account);
    IVerifyPinResponse? AuthVerifyPin(IAuthAccount account, string pinCode);
    IGetHomeScreenResponse? SystemGetHomeScreen(IAuthAccount account);
    byte[]? DownloadBytes(string url);
    ICommandStatusResponse? SystemTriggerCameraSnapshot(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
    ICommandStatusResponse? SystemTriggerDoorbellSnapshot(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
}