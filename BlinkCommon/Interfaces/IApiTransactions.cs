namespace BlinkCommon.Interfaces;

public interface IApiTransactions
{
    void SetAccessToken(string accessToken);
    ILoginResponse? AuthLogin();
    ILogoutResponse? AuthLogout(IAccount account);
    IVerifyPinResponse? AuthVerifyPin(IAccount account, string pinCode);
}