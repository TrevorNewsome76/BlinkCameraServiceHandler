using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests;

public class MockSettings
{
    public static IBlinkSettings CreateSettings() => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = "test@email.com",
        Password = "PAssword",
        HardwareId = string.Empty,
        WatchIntervalSeconds = 20,
        LocalWatchIntervalSeconds = 5,
    });

    public static IBlinkSettings CreateSettings(string email, string password) => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = email,
        Password = password,
        HardwareId = string.Empty,
        WatchIntervalSeconds = 20,
        LocalWatchIntervalSeconds = 5,
    });

    public static IBlinkSettings CreateTwoFactorSettings() => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = "twofactor@email.com",
        Password = "PAssword",
        HardwareId = string.Empty,
        WatchIntervalSeconds = 20,
        LocalWatchIntervalSeconds = 5,
    });

    public static IBlinkSettings CreateNoThumbnailSettings() => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = "nothumbnail@email.com",
        Password = "PAssword",
        HardwareId = string.Empty,
        WatchIntervalSeconds = 20,
        LocalWatchIntervalSeconds = 5,
    });

    // An account whose home screen lists sync modules, but none with active local storage —
    // distinct from an account that simply never ran MAP/GET (SyncModules null).
    public static IBlinkSettings CreateNoActiveLocalStorageSettings() => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = "noactivelocal@email.com",
        Password = "PAssword",
        HardwareId = string.Empty,
        WatchIntervalSeconds = 20,
        LocalWatchIntervalSeconds = 5,
    });
}
