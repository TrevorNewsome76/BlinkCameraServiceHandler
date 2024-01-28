using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkApiTransactions(IBlinkSettings? settings) : IBlinkApiTransactions
{
    private static IApiMethods ApiDriver => Shelf.RetrieveInstance<IApiMethods>();

    private IBlinkSettings BlinkSettings { get; } = settings ?? Duck.Implement<IBlinkSettings>();

    public void SetAccessToken(string accessToken) =>
        ApiDriver.SetAccessToken(accessToken);

    public ILoginResponse? AuthLogin()
    {
        var parameters = new List<KeyValuePair<string, string>>();

        parameters
            .Add(new KeyValuePair<string, string>("email",
                BlinkSettings.Email));

        parameters
            .Add(new KeyValuePair<string, string>("password",
                BlinkSettings.Password));

        var result =
            ApiDriver?
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v5/account/login",
                    parameters) ?? string.Empty;

        var resultObject = result.Deserialize<ILoginResponse>();

        

        return resultObject;
    }

    public ILogoutResponse? AuthLogout(string accountId, string clientId)
    {
        var result =
            ApiDriver?
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v4/account/{accountId}/client/{clientId}/logout") ?? string.Empty;

        return result.Deserialize<ILogoutResponse>();
    }

    public IVerifyPinResponse AuthVerifyPin(string pinCode, string tier, long accountId, long clientId, string accessToken)
    {
        var baseString = $"https://rest-{tier}.immedia-semi.com/api/v4/account/{accountId}/client/{clientId}/pin/verify";
        var serializedPin = "{\"pin\":\"" +pinCode + "\"}";
        var result = ApiDriver?.Post(baseString, serializedPin) ?? string.Empty;
        return result.Deserialize<IVerifyPinResponse>();
    }
}