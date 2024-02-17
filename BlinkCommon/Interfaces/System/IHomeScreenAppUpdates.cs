namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenAppUpdates
{
    string Message { get; }
    int Code { get; }
    bool Update_Available { get; }
    bool Update_Required { get; }
}