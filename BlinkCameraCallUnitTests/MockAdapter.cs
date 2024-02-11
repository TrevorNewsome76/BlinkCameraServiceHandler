using BlinkCameraCall;
using BlinkCommon.Interfaces;
using Dependency;

namespace BlinkCameraCallUnitTests;

public class MockAdapter : IAdapter
{
    public MockAdapter(IBlinkSettings configuration)
    {
        Shelf.Clear();
        Shelf.ShelveInstance(MockApiDriver.MockHttpClientApiDriver());
        Shelf.ShelveInstance(ApiInterface.Initialize(configuration));
    }
}