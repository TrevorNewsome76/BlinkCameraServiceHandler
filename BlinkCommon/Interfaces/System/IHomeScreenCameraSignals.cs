namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenCameraSignals
{
    int Lfr { get; }
    int Wifi { get; }
    int Temp { get; }
    int Battery { get; }
}