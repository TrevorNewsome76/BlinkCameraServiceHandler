using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests;

public class MockSettings
{
    public static IBlinkSettings CreateSettings() => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "http://localhost",
        Email = "test@email.com",
        Password = "password",
        GrantType = "client_credentials",
        Accept = "application/json",
    });

    public static IBlinkSettings CreateSettings(string email, string password) => Duck.Implement<IBlinkSettings>(new
    {
        BaseUrl = "http://localhost",
        Email = email,
        Password = password,
        GrantType = "client_credentials",
        Accept = "application/json",
    });
}