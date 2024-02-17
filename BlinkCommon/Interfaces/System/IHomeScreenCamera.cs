
namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenCamera
{
    long id { get; }
    DateTime Created_At { get; }
    DateTime Updated_At { get; }
    string Name { get; }
    string Serial { get; }
    string Fw_Version { get; }
    string Type { get; }
    bool Enabled { get; }
    string Thumbnail { get; }
    string Status { get; }
    string Battery { get; }
    bool Usage_Rate { get; }
    long Network_Id { get; }
    string[] Issues { get; }
    IHomeScreenCameraSignals Signals { get; }
    bool Local_Storage_Enabled { get; }
    bool Local_Storage_Compatible { get; }
    bool Snooze { get; }
    string? Snooze_Time_Remaining { get; }
    string Revision { get; }
    string Color { get; }
}