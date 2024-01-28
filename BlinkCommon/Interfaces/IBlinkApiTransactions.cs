namespace BlinkCommon.Interfaces;

public interface IBlinkApiTransactions
{
    void SetAccessToken(string accessToken);
    ILoginResponse? AuthLogin();
    ILogoutResponse? AuthLogout(string account, string client);
    IVerifyPinResponse? AuthVerifyPin(string pinCode, string tier, long accountId, long clientId, string accessToken);
}