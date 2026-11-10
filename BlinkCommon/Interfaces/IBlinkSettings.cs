namespace BlinkCommon.Interfaces;

public interface IBlinkSettings
{
    string Email{ get; set; }

    string Password { get; set; }

    string BaseUrl { get; set; }

    /// <summary>
    /// A persisted device id (UUID) sent as hardware_id during Blink's OAuth login flow.
    /// Keeping this stable across runs lets Blink recognise the device, avoiding a
    /// repeated two-factor challenge on every login.
    /// </summary>
    string HardwareId { get; set; }

    /// <summary>
    /// Polling interval, in seconds, used when watching Blink's cloud (WATCH).
    /// Defaults to 20 when absent from the settings file.
    /// </summary>
    int WatchIntervalSeconds { get; set; }

    /// <summary>
    /// Polling interval, in seconds, used when watching local storage (LOCALWATCH).
    /// Defaults to 5 when absent from the settings file.
    /// </summary>
    int LocalWatchIntervalSeconds { get; set; }

}