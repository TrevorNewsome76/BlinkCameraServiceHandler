namespace BlinkCommon.Interfaces;

public interface IBlinkApiTransactions
{
    ILoginResponse? AuthLogin();
    ILogoutResponse? AuthLogout(string account, string client);
    string? AuthVerifyPin();
}