namespace BlinkCommon.Interfaces;

public interface IVerifyPinResponse
{
    string? Message { get; }
    int? Code { get; }
    bool Valid { get; }
    bool Require_new_pin { get; }
}