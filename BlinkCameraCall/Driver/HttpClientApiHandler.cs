using System.Net.Http.Headers;
using System.Text;
using BlinkCommon.Interfaces;

namespace BlinkCameraCall.Driver;

internal class HttpClientApiHandler : IApiMethods, IDisposable
{
    private readonly HttpClientHandler _handler = new()
    {
        UseDefaultCredentials = true,
        AllowAutoRedirect = true,
    };

    private HttpClient _httpClient;

    public HttpClientApiHandler()
    {
        _httpClient = new HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
        };
    }

    public HttpClientApiHandler(HttpMessageHandler messageHandler)
    {
        _httpClient = new HttpClient(messageHandler)
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
        };
    }

    public bool SetAccessToken(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        return _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("TOKEN_AUTH", accessToken);
    }

    public string Post(string url, List<KeyValuePair<string, string>> parameters) =>
        _httpClient.PostAsync(url, new FormUrlEncodedContent(parameters)).Result.Content.ReadAsStringAsync().Result;

    public string Post(string url, string serializedJsonString) =>
        _httpClient.PostAsync(url, new StringContent(serializedJsonString, Encoding.UTF8, "application/json"))
            .Result.Content.ReadAsStringAsync().Result;

    public string Post(string url) =>
        _httpClient.PostAsync(url, new StringContent(string.Empty, Encoding.UTF8, "application/json"))
            .Result.Content.ReadAsStringAsync().Result;

    public string Put(string url, string serializedJsonString) =>
        _httpClient.PutAsync(url, new StringContent(serializedJsonString, Encoding.UTF8, "application/json"))
            .Result.Content.ReadAsStringAsync().Result;

    public string Get(string url) => _httpClient.GetAsync(url).Result.Content.ReadAsStringAsync().Result;

    void IDisposable.Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _httpClient = null!;
    }
}