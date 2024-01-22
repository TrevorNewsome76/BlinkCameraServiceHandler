using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkApiTransactions : IBlinkApiTransactions
{
    private const string BaseUrl = "https://stagingapi.enquirymax.net";
    private IApiMethods ApiDriver => Shelf.RetrieveInstance<IApiMethods>();

    private IBlinkSettings BlinkSettings { get; }

    public BlinkApiTransactions(IBlinkSettings settings)
    {
        BlinkSettings = settings;
    }

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

        return result.Deserialize<ILoginResponse>();
    }

    public ILogoutResponse? AuthLogout(string accountId, string clientId)
    {
        var result =
            ApiDriver?
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/api/v4/account/{accountId}/client/{clientId}/logout") ?? string.Empty;

        return result.Deserialize<ILogoutResponse>();
    }

    public string? AuthVerifyPin()
    {
        throw new NotImplementedException();
    }
}