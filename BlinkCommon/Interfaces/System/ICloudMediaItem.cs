namespace BlinkCommon.Interfaces.System;

public interface ICloudMediaItem
{
    long Id { get; }
    long Network_Id { get; }
    long Device_Id { get; }
    string Device_Name { get; }
    DateTime Created_At { get; }
    string Media { get; }
    bool Deleted { get; }
}
