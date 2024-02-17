using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class AdapterExtension
{
    private static IApiTransactions Transactions =>
        Shelf.RetrieveInstance<IApiTransactions>()
        ?? Duck.Implement<IApiTransactions>();

    public static ILoginResponse Login(this IAdapter adapter) =>
        Transactions.AuthLogin() ?? Duck.Implement<ILoginResponse>(new());

    public static ILoginResponse Login(this IAdapter adapter, string username, string password) =>
        Transactions.AuthLogin(username, password) ?? Duck.Implement<ILoginResponse>(new());

    public static void SetAccessToken(this IAdapter adapter, string token) =>
        Transactions.SetAccessToken(token);

    public static ILogoutResponse Logout(this IAdapter adapter, IAuthAccount account) =>
        Transactions?.AuthLogout(account) ?? Duck.Implement<ILogoutResponse>(new());

    public static IVerifyPinResponse
        VerifyPin(this IAdapter adapter, IAuthAccount account, string pinCode) =>
        Transactions.AuthVerifyPin(account, pinCode) ?? Duck.Implement<IVerifyPinResponse>();

    public static IGetHomeScreenResponse
        GetHomeScreen(this IAdapter adapter, IAuthAccount account) =>
        Transactions.SystemGetHomeScreen(account) ?? Duck.Implement<IGetHomeScreenResponse>();
}