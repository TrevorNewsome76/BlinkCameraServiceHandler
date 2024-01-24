namespace BlinkCommon.Interfaces;

public interface IBlinkApiTransactions
{
    ILoginResponse? AuthLogin();
    ILogoutResponse? AuthLogout(string account, string client);
    IVerifyPinResponse? AuthVerifyPin(string pinCode, string tier, long accountId, long clientId, string accessToken);
}