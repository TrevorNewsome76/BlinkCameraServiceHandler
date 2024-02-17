namespace BlinkCommon.Interfaces.Auth;

public interface IVerifyPhone
{
    bool Required { get; }
    string Channel { get; }
}