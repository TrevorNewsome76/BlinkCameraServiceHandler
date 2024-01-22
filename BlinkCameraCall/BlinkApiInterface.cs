using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkApiInterface
{
    public static IBlinkApiTransactions Initialize(IBlinkSettings configuration)
    {
        configuration ??= Duck.Implement<IBlinkSettings>(new());
        return new BlinkApiTransactions(configuration);
    }
}