namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenNetwork
{
    long Id { get; }   
    DateTime Created_At { get; }
    DateTime Updated_At { get; }
    string Name { get; }
    string TimeZone { get; }
    bool Dts { get; }
    bool Armed { get; }
    bool Lv_Save { get; }
}