using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;

namespace BlinkCameraCallUnitTests;

public static class MockApiDriver
{
    public static IApiMethods MockHttpClientApiDriver() => new MockHttpClientApiHandler();
}

internal class MockHttpClientApiHandler : IApiMethods, IDisposable
{
    public bool TokenAuthorised { get; set; } = false;

    public string Post(string Url, List<KeyValuePair<string, string>> parameters)
    {
        if (string.IsNullOrEmpty(Url)) throw new ArgumentException("Url cannot be empty or null.");

        switch (Url)
        {
            case
                "http://localhost/api/v5/account/login":
                return MockData.AuthLoginResponse().Serialize();
            default:
                throw new NotImplementedException();
        }

        throw new NotImplementedException();
    }

    public string? Post(string Url, string body)
    {
        throw new NotImplementedException();
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