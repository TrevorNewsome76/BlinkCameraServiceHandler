using System.Net.Http.Headers;
using System.Text;
using BlinkCommon.Interfaces;

namespace BlinkCameraCall.Driver;

internal class HttpClientApiHandler : IApiMethods, IDisposable
{
    private readonly HttpClientHandler _handler = new HttpClientHandler()
    {
        UseDefaultCredentials = true,
        AllowAutoRedirect = true,
    };

    private System.Net.Http.HttpClient _httpClient;

    public HttpClientApiHandler()
    {
        _httpClient = new System.Net.Http.HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
        };
    }

    public HttpClientApiHandler(HttpMessageHandler messageHandler)
    {
        _httpClient = new System.Net.Http.HttpClient(messageHandler)
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
        };
    }

    public void SetAccessToken(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Clear();

        var result = _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("TOKEN_AUTH", accessToken);
        if (!result) throw new Exception("Access token failed to be set in request headers");
    }

    public string Post(string Url, List<KeyValuePair<string, string>> parameters) =>
        _httpClient.PostAsync(Url, new FormUrlEncodedContent(parameters)).Result.Content.ReadAsStringAsync().Result;

    public string Post(string Url, string serializedJsonString) =>
        _httpClient.PostAsync(Url, new StringContent(serializedJsonString, Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result;

    public string Post(string Url) =>
        _httpClient.PostAsync(Url, new StringContent(string.Empty, Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result;

    public string Put(string Url, string serializedJsonString) =>
        _httpClient.PutAsync(Url, new StringContent(serializedJsonString, Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result;

    public string Get(string Url) => _httpClient.GetAsync(Url).Result.Content.ReadAsStringAsync().Result;

    void IDisposable.Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _httpClient = null!;
    }
}