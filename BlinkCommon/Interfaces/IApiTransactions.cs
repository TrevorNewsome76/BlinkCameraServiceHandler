using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;

namespace BlinkCommon.Interfaces;

public interface IApiTransactions
{
    bool SetAccessToken(string accessToken);
    ILoginResponse? AuthLogin();
    ILoginResponse? AuthLogin(string username, string password);
    ILogoutResponse? AuthLogout(IAuthAccount account);
    IVerifyPinResponse? AuthVerifyPin(IAuthAccount account, string pinCode);
    IGetHomeScreenResponse? SystemGetHomeScreen(IAuthAccount account);
}