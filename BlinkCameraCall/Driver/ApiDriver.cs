using BlinkCommon.Interfaces;

namespace BlinkCameraCall.Driver;

public class ApiDriver
{
    public static IApiMethods HttpClientApiHandler() =>
        new HttpClientApiHandler();

    public static IApiMethods HttpClientApiHandler(HttpMessageHandler messageHandler) =>
        new HttpClientApiHandler(messageHandler);
}

