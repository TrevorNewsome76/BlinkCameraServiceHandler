namespace BlinkCommon.Interfaces;

public interface IApiTransactions
{
    bool SetAccessToken(string accessToken);
    ILoginResponse? AuthLogin();
    ILogoutResponse? AuthLogout(IAccount account);
    IVerifyPinResponse? AuthVerifyPin(IAccount account, string pinCode);
}