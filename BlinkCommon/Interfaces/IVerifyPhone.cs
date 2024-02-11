namespace BlinkCommon.Interfaces;

public interface IVerifyPhone
{
    bool Required { get; }
    string Channel { get; }
}