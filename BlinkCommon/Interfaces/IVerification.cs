namespace BlinkCommon.Interfaces;

public interface IVerification
{
    IVerifyEmail Email { get; }
    IVerifyPhone Phone { get; }
}