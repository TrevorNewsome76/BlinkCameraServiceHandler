namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenDeviceLimits
{
    int Camera { get; }
    int Chime { get; }
    int Doorbell { get; }
    int Doorbell_Button { get; }
    int Owl { get; }
    int Siren { get; }
    int Total_Devices { get; }
}
