namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenVideoStats
{
    int Storage { get; }
    int Auto_Delete_Days { get; }
    int[] Auto_Delete_Day_Options { get; }
}