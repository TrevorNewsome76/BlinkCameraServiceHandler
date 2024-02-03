using BlinkCameraCall;
using BlinkCommon.Interfaces;
using Dependency;

namespace BlinkCameraCallUnitTests;

public class MockAdapter
{
    public MockAdapter(IBlinkSettings configuration)
    {
        Shelf.Clear();
        Shelf.ShelveInstance(MockApiDriver.MockHttpClientApiDriver());
        Shelf.ShelveInstance(BlinkApiInterface.Initialize(configuration));
    }
}