using BlinkCommon.Interfaces;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class AdapterExtension
{
    private static IApiTransactions Transactions =>
        Shelf.RetrieveInstance<IApiTransactions>()
        ?? Duck.Implement<IApiTransactions>();

    public static ILoginResponse Login(this IAdapter adapter, string username, string password) =>
        Transactions.AuthLogin(username, password) ?? Duck.Implement<ILoginResponse>(new());

    public static void SetAccessToken(this IAdapter adapter, string token) =>
        Transactions.SetAccessToken(token);

    public static ILogoutResponse Logout(this IAdapter adapter, IAccount account) =>
        Transactions?.AuthLogout(account) ?? Duck.Implement<ILogoutResponse>(new());

    public static IVerifyPinResponse
        VerifyPin(this IAdapter adapter, IAccount account, string pinCode) =>
        Transactions.AuthVerifyPin(account, pinCode) ?? Duck.Implement<IVerifyPinResponse>();
}