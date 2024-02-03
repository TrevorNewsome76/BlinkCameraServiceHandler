using BlinkCommon;
using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkApiTransactions(IBlinkSettings settings) : IApiTransactions
{
    private static IApiMethods ApiDriver => Shelf.RetrieveInstance<IApiMethods>();

    private IBlinkSettings BlinkSettings { get; } = settings;

    public void SetAccessToken(string accessToken) =>
        ApiDriver.SetAccessToken(accessToken);

    public ILoginResponse? AuthLogin()
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("email",
                BlinkSettings.Email),
            new KeyValuePair<string, string>("password",
                BlinkSettings.Password)
        };

        var result =
            ApiDriver
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v5/account/login",
                    parameters);

        return result?.Deserialize<ILoginResponse>() ?? Duck.Implement<ILoginResponse>();
    }

    public ILogoutResponse? AuthLogout(IAccount account)
    {
        var result =
            ApiDriver?
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v4/account/{account.Account_Id}/client/{account.Client_Id}/logout") ?? string.Empty;

        return result.Deserialize<ILogoutResponse>();
    }

    public IVerifyPinResponse AuthVerifyPin(IAccount account, string pinCode)
    {
        var baseString = $"https://rest-{account.Tier}.immedia-semi.com/api/v4/account/{account.Account_Id}/client/{account.Client_Id}/pin/verify";
        var serializedPin = "{\"pin\":\"" +pinCode + "\"}";
        var result = ApiDriver?.Post(baseString, serializedPin) ?? string.Empty;
        return result.Deserialize<IVerifyPinResponse>();
    }
}