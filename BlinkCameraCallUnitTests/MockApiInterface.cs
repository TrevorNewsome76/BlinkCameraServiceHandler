using BlinkCameraCall;
using BlinkCommon.Interfaces;
using BlinkCommon;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests;

public class MockApiInterface : IApiInterface
{
    public IApiTransactions Initialize(IBlinkSettings configuration) =>
        Duck.Implement<IApiTransactions>(new ApiTransactions(configuration));
}