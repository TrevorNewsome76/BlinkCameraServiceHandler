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
    ICommandStatusResponse? SystemEnableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
    ICommandStatusResponse? SystemDisableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120);
    ICommandStatusResponse? SystemEnableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
    ICommandStatusResponse? SystemDisableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120);
    ICommandStatusResponse? SystemArmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120);
    ICommandStatusResponse? SystemDisarmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120);
    IMediaChangedResponse? SystemGetRecentMedia(IAuthAccount account, DateTime sinceUtc);
    IManifestRequestResponse? SystemRequestLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId);
    ILocalStorageManifestResult? SystemGetLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId, long manifestRequestId, TimeSpan? pollInterval = null, int maxRetries = 120);
    string BuildLocalStorageClipUrl(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId);
    ICommandStatusResponse? SystemPrepareLocalStorageClip(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId, TimeSpan? pollInterval = null, int maxRetries = 120);
}