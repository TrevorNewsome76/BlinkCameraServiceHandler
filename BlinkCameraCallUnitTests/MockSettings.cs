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
    });

    public static IBlinkSettings CreateSettings(string email, string password) => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "https://rest-prod.immedia-semi.com",
        Email = email,
        Password = password,
    });
}