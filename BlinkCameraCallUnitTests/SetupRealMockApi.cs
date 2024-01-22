using BlinkCameraCall;
using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using Dependency;

namespace BlinkCameraCallUnitTests;

public class MockAdapter
{
    public static void Initialize(IBlinkSettings configuration)
    {
        Shelf.Clear();
        Shelf.ShelveInstance(BlinkApiInterface.Initialize(configuration));
        Shelf.ShelveInstance(MockApiDriver.MockHttpClientApiDriver());
    }
}