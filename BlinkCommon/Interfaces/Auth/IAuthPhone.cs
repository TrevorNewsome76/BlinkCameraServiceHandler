﻿namespace BlinkCommon.Interfaces.Auth;

public interface IAuthPhone
{
    string Number { get; }
    string Last_4_Digits { get; }
    string Country_Calling_Code { get; }
    bool Valid { get; }

}