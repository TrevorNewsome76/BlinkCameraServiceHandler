namespace BlinkCommon.Interfaces;

public interface IApiTransactions
{
    bool SetAccessToken(string accessToken);
    ILoginResponse? AuthLogin();
    ILoginResponse? AuthLogin(string username, string password);
    ILogoutResponse? AuthLogout(IAccount account);
    IVerifyPinResponse? AuthVerifyPin(IAccount account, string pinCode);
}