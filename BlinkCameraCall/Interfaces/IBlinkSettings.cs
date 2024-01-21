namespace BlinkCameraCall.Interfaces;

public interface IBlinkSettings
{
    string ClientId { get; set; }

    string BaseUrl { get; set; }

    string Region { get; set; }
}