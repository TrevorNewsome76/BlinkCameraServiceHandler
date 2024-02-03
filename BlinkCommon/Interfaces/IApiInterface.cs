namespace BlinkCommon.Interfaces;

public  interface IApiInterface
{
    IApiTransactions Initialize(IBlinkSettings configuration);
}