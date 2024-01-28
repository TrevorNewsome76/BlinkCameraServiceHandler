using BlinkCommon.Interfaces;

namespace BlinkCameraCallUnitTests;

public static class MockApiDriver
{
    public static IApiMethods MockHttpClientApiDriver() => 
        new MockHttpClientApiHandler();
}