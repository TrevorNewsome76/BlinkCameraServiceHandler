using BlinkCommon;
using BlinkCommon.Interfaces;

namespace BlinkCameraCall;

public class ApiInterface
{
    public static IApiTransactions Initialize(IBlinkSettings configuration) => 
        new ApiTransactions(configuration);
}