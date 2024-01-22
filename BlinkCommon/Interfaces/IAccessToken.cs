namespace BlinkCommon.Interfaces;

public interface IAccessToken
{
    string? AccessToken { get; set; }
    string? AccountId { get; set; }
    string ClientId { get; set; }
    string Tier { get; set; }
}
