namespace BlinkCameraCall.Interfaces;

public interface IApiMethods
{
    void SetAccessToken(KeyValuePair<string, string> accessToken);
    string? Post(string Url, List<KeyValuePair<string, string>> parameters);
    string? Post(string Url, string body);
    string? Post(string Url);
    string? Put(string Url, string serializedJsonString);
    string? Get(string Url);
}