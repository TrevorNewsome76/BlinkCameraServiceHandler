namespace BlinkCameraCall.Interfaces;

public interface IBlinkApiTransactions
{
    IAccessToken? RetrieveAccessToken();

    string? AuthLogin(IEnumerable<KeyValuePair<string, string>> parameters);

    string? AuthLogout(IEnumerable<KeyValuePair<string, string>> parameters);
    string? AuthVerifyPin(IEnumerable<KeyValuePair<string, string>> parameters);
}