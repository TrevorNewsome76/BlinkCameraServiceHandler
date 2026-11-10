using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class AdapterExtension
{
    private static IApiTransactions Transactions =>
        Shelf.RetrieveInstance<IApiTransactions>()
        ?? Duck.Implement<IApiTransactions>();

    public static ILoginResponse Login(this IAdapter adapter) =>
        Transactions.AuthLogin() ?? Duck.Implement<ILoginResponse>(new());

    public static ILoginResponse Login(this IAdapter adapter, string username, string password) =>
        Transactions.AuthLogin(username, password) ?? Duck.Implement<ILoginResponse>(new());

    public static ILoginResponse CompleteTwoFactorLogin(this IAdapter adapter, string code) =>
        Transactions.AuthCompleteTwoFactorLogin(code) ?? Duck.Implement<ILoginResponse>(new());

    public static void SetAccessToken(this IAdapter adapter, string token) =>
        Transactions.SetAccessToken(token);

    public static ILogoutResponse Logout(this IAdapter adapter, IAuthAccount account) =>
        Transactions?.AuthLogout(account) ?? Duck.Implement<ILogoutResponse>(new());

    public static IVerifyPinResponse
        VerifyPin(this IAdapter adapter, IAuthAccount account, string pinCode) =>
        Transactions.AuthVerifyPin(account, pinCode) ?? Duck.Implement<IVerifyPinResponse>();

    public static IGetHomeScreenResponse
        GetHomeScreen(this IAdapter adapter, IAuthAccount account) =>
        Transactions.SystemGetHomeScreen(account) ?? Duck.Implement<IGetHomeScreenResponse>();

    public static byte[]? DownloadBytes(this IAdapter adapter, string url) =>
        Transactions.DownloadBytes(url);

    public static ICommandStatusResponse? TriggerCameraSnapshot(this IAdapter adapter, IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
        Transactions.SystemTriggerCameraSnapshot(account, networkId, cameraId, pollInterval, maxRetries);

    public static ICommandStatusResponse? TriggerDoorbellSnapshot(this IAdapter adapter, IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
        Transactions.SystemTriggerDoorbellSnapshot(account, networkId, doorbellId, pollInterval, maxRetries);

    public static ICommandStatusResponse? EnableCamera(this IAdapter adapter, IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
        Transactions.SystemEnableCamera(account, networkId, cameraId, pollInterval, maxRetries, cancellationToken);

    public static ICommandStatusResponse? DisableCamera(this IAdapter adapter, IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
        Transactions.SystemDisableCamera(account, networkId, cameraId, pollInterval, maxRetries, cancellationToken);

    public static ICommandStatusResponse? EnableDoorbell(this IAdapter adapter, IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
        Transactions.SystemEnableDoorbell(account, networkId, doorbellId, pollInterval, maxRetries, cancellationToken);

    public static ICommandStatusResponse? DisableDoorbell(this IAdapter adapter, IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
        Transactions.SystemDisableDoorbell(account, networkId, doorbellId, pollInterval, maxRetries, cancellationToken);

    public static ICommandStatusResponse? ArmNetwork(this IAdapter adapter, IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
        Transactions.SystemArmNetwork(account, networkId, pollInterval, maxRetries, cancellationToken);

    public static ICommandStatusResponse? DisarmNetwork(this IAdapter adapter, IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
        Transactions.SystemDisarmNetwork(account, networkId, pollInterval, maxRetries, cancellationToken);

    public static IMediaChangedResponse? GetRecentMedia(this IAdapter adapter, IAuthAccount account, DateTime sinceUtc) =>
        Transactions.SystemGetRecentMedia(account, sinceUtc);

    public static IManifestRequestResponse? RequestLocalStorageManifest(this IAdapter adapter, IAuthAccount account, long networkId, long syncModuleId) =>
        Transactions.SystemRequestLocalStorageManifest(account, networkId, syncModuleId);

    public static ILocalStorageManifestResult? GetLocalStorageManifest(this IAdapter adapter, IAuthAccount account, long networkId, long syncModuleId, long manifestRequestId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
        Transactions.SystemGetLocalStorageManifest(account, networkId, syncModuleId, manifestRequestId, pollInterval, maxRetries);

    public static string BuildLocalStorageClipUrl(this IAdapter adapter, IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId) =>
        Transactions.BuildLocalStorageClipUrl(account, networkId, syncModuleId, manifestId, clipId);

    public static ICommandStatusResponse? PrepareLocalStorageClip(this IAdapter adapter, IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId, TimeSpan? pollInterval = null, int maxRetries = 120) =>
        Transactions.SystemPrepareLocalStorageClip(account, networkId, syncModuleId, manifestId, clipId, pollInterval, maxRetries);
}