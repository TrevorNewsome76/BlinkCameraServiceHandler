using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;

namespace BlinkCameraCallUnitTests;

internal class MockHttpClientApiHandler : IApiMethods, IDisposable
{
    public string AccessToken { get; set; } = string.Empty;

    public bool SetAccessToken(string accessToken)
    {
        if (accessToken == "BU8fOjaF4E5POf4WTRm5wA")
        {
            AccessToken = accessToken;
            return true;
        }
        else
        {
            return false;
        }
    }

    public string Post(string url, List<KeyValuePair<string, string>> parameters)
    {
        switch (url)
        {
            case "https://rest-prod.immedia-semi.com/api/v5/account/login":
                return (parameters[0].Value == MockSettings.CreateSettings().Email 
                        && parameters[1].Value == MockSettings.CreateSettings().Password)
                    ? MockData.AuthLoginCorrectResponse().Serialize()
                    : MockData.AuthLoginFailedResponse().Serialize();
            default: throw new NotImplementedException();
        };
    }

    public string? Post(string Url, string body)
    {
        return Url switch
        {
            "https://rest-007.immedia-semi.com/api/v4/account/134166/client/1486061/pin/verify" =>
                body == "{\"pin\":\"987654\"}"
                    ? MockData.AuthCorrectPinResponse().Serialize()
                    : MockData.AuthIncorrectPinResponse().Serialize(),
            _ => throw new NotImplementedException()
        };
    }

    public string? Post(string Url)
    {
        switch (Url)
        {
            case "https://rest-prod.immedia-semi.com/api/v4/account/134166/client/1486061/logout":
                return MockData.AuthLogoutCorrectResponse().Serialize();
                break;
            default:
                return MockData.AuthLogoutIncorrectResponse().Serialize();
                break;
        }
    }

    public string? Put(string Url, string serializedJsonString)
    {
        throw new NotImplementedException();
    }

    public string? Get(string Url)
    {
        switch (Url)
        {
            case "https://rest-007.immedia-semi.com/api/v3/accounts/134166/homescreen":
                return MockData.CreateValidHomeScreenResponse().Serialize();
                break;
            default:
                return MockData.AuthLogoutIncorrectResponse().Serialize();
                break;
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
