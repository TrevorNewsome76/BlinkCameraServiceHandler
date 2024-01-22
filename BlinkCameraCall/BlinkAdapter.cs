using BlinkCameraCall.Driver;
using System;
using BlinkCommon.Interfaces;
using Dependency;

namespace BlinkCameraCall;

public class BlinkAdapter
{
    public static void Initialize(IBlinkSettings configuration)
    {
        Shelf.ShelveInstance(BlinkApiInterface.Initialize(configuration));
        Shelf.ShelveInstance(ApiDriver.HttpClientApiHandler());
    }
}