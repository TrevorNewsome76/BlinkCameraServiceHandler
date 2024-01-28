using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;

namespace BlinkCameraCallUnitTests;

internal class MockHttpClientApiHandler : IApiMethods, IDisposable
{
    public bool TokenAuthorized { get; set; } = false;

    public string AccessToken { get; set; } = string.Empty;

    public void SetAccessToken(string accessToken)
    {
        if (accessToken == "BU8fOjaF4E5POf4WTRm5wA")
            AccessToken = accessToken;
    }

    public string Post(string url, List<KeyValuePair<string, string>> parameters)
    {
        if (string.IsNullOrEmpty(url)) throw new ArgumentException("Url cannot be empty or null.");

        return url switch
        {
            "http://localhost/api/v5/account/login" => (parameters[0].Value == "test@email.com" &&
                                                        parameters[1].Value == "password")
                ? MockData.AuthLoginCorrectResponse().Serialize()
                : MockData.AuthLoginFailedResponse().Serialize(),
            _ => throw new NotImplementedException()
        };
    }

    public string? Post(string Url, string body)
    {
        if (string.IsNullOrEmpty(Url)) throw new ArgumentException("Url cannot be empty or null.");

        return Url switch
        {
            "https://rest-007.immedia-semi.com/api/v4/account/123456/client/7890/pin/verify" =>
                body == "{\"pin\":\"987654\"}"
                    ? MockData.AuthCorrectPinResponse().Serialize()
                    : MockData.AuthIncorrectPinResponse().Serialize(),
            _ => throw new NotImplementedException()
        };
    }

    public string? Post(string Url)
    {
        throw new NotImplementedException();
    }

    public string? Put(string Url, string serializedJsonString)
    {
        throw new NotImplementedException();
    }

    public string? Get(string Url)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
