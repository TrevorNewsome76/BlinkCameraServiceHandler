using BlinkCameraCall.Driver;
using System;
using BlinkCameraCall.Interfaces;
using Dependency;

namespace BlinkCameraCall;

public class BlinkAdapter
{
    public static IAccessToken? AccessToken;

    public static void Initialize(IBlinkSettings configuration)
    {
        Shelf.ShelveInstance(BlinkApiInterface.Initialize(configuration));
        Shelf.ShelveInstance(ApiDriver.HttpClientApiHandler());
    }
}