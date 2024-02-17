namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenSyncModule
{
    long Id { get; }
    DateTime Created_At { get; }
    DateTime Updated_At { get; }
    bool Onboarded { get; }
    string Status { get; }
    string Name { get; }
    string Serial { get; }
    string Fw_Version { get; }
    string Type { get; }
    string SubType { get; }
    string Last_Hb { get; }
    int Wifi_Strength { get; }
    string Network_Id { get; }
    bool Enable_Temp_Alerts { get; }
    bool Local_Storage_Enabled { get; }
    bool Local_Storage_Compatible { get; }
    string Local_Storage_Status { get; }
    string Revision { get; }
}