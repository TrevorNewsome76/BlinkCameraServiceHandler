namespace BlinkCommon.Interfaces.System;

public interface ILocalStorageClip
{
    long Id { get; }
    string Camera_Name { get; }
    DateTime Created_At { get; }
    long Size { get; }
}
