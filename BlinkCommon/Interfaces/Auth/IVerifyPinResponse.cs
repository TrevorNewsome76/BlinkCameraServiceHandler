﻿namespace BlinkCommon.Interfaces.Auth;

public interface IVerifyPinResponse
{
    string? Message { get; }
    int? Code { get; }
    bool Valid { get; }
    bool Require_New_Pin { get; }
}