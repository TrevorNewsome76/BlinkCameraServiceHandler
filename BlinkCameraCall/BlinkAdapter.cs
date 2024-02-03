using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using Dependency;

namespace BlinkCameraCall;

public class BlinkAdapter : IAdapter
{
    public BlinkAdapter(IBlinkSettings configuration)
    {
        Shelf.Clear();
        Shelf.ShelveInstance(ApiDriver.HttpClientApiHandler());
        Shelf.ShelveInstance(ApiInterface.Initialize(configuration));

    }
}