using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using Dependency;

namespace BlinkCameraCall;

public class BlinkAdapter
{
    public static void Initialize(IBlinkSettings configuration)
    {
        Shelf.Clear();
        Shelf.ShelveInstance(BlinkApiInterface.Initialize(configuration));
        Shelf.ShelveInstance(ApiDriver.HttpClientApiHandler());
    }
}