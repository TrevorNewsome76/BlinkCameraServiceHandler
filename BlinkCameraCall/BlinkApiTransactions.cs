using BlinkCameraCall.Extensions;
using BlinkCameraCall.Interfaces;
using Dependency;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkApiTransactions : IBlinkApiTransactions
{
    private const string BaseUrl = "https://stagingapi.enquirymax.net";
    private IApiMethods ApiDriver => Shelf.RetrieveInstance<IApiMethods>();

    public IBlinkSettings BlinkSettings { get; private set; }

    public BlinkApiTransactions(IBlinkSettings settings)
    {
        BlinkSettings = settings;
    }

    public IAccessToken RetrieveAccessToken()
    {
        var parameters = new List<KeyValuePair<string, string>>();

        parameters
            .Add(new KeyValuePair<string, string>("clientId",
                BlinkSettings.ClientId));

        var result =
            ApiDriver?
                .Post($"{BlinkSettings?.BaseUrl ?? string.Empty}/services/token",
                    parameters) ?? string.Empty;

        var accessToken = result.Deserialize<IAccessToken>();
        ApiDriver?.SetAccessToken(new KeyValuePair<string, string>("Bearer",
            accessToken?.AccessToken ?? string.Empty));
        return accessToken ?? Duck.Implement<IAccessToken>(new());
    }

    public string? AuthLogin(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        throw new NotImplementedException();
    }

    public string? AuthLogout(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        throw new NotImplementedException();
    }

    public string? AuthVerifyPin(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        throw new NotImplementedException();
    }
}