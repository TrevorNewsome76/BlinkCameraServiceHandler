using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;

namespace BlinkCameraCallUnitTests;

public static class MockApiDriver
{
    public static IApiMethods MockHttpClientApiDriver() =>
        new MockHttpClientApiHandler();

    public static IOAuthLogin MockOAuthLoginDriver() =>
        new MockOAuthLoginClient();
}