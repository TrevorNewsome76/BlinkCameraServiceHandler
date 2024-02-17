namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenDoorbells
{
    long Id { get; }
    DateTime Created_At { get; }
    DateTime Updated_At { get; }
    string Name { get; }
    string Type { get; }
    bool Onboarded { get; }
    string Serial { get; }
    string Fw_Version { get; }
    bool Enabled { get; }
    string Thumbnail { get; }
    string Status { get; }
    string Battery { get; }
    string Doorbell_Mode { get; }
    bool Changing_Mode { get; }
    IHomeScreenCameraSignals Signals { get; }
    string[] Issues { get; }
    bool Local_Storage_Enabled { get; }
    bool Local_Storage_Compatible { get; }
    bool Config_Out_Of_Sync { get; }
    bool Snooze { get; }
    string? Snooze_Time_Remaining { get; }
    string Revision { get; }
    string Color { get; }
}