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
    });

    public static IBlinkSettings CreateSettings(string email, string password) => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = email,
        Password = password,
        HardwareId = string.Empty,
    });

    public static IBlinkSettings CreateTwoFactorSettings() => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = "twofactor@email.com",
        Password = "PAssword",
        HardwareId = string.Empty,
    });
}
