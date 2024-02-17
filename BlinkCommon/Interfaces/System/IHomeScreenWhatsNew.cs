using System.Runtime.InteropServices.JavaScript;

namespace BlinkCommon.Interfaces.System;

public interface IHomeScreenWhatsNew
{
    DateOnly Updated_At { get; }
    string Url { get; }
}