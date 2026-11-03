using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;

namespace BlinkCameraCall.Driver;

public class ApiDriver
{
    public static IApiMethods HttpClientApiHandler() =>
        new HttpClientApiHandler();

    public static IOAuthLogin OAuthLoginHandler() =>
        new OAuthLoginClient();

    //public static IApiMethods HttpClientApiHandler(HttpMessageHandler messageHandler) =>
    //    new HttpClientApiHandler(messageHandler);
}

