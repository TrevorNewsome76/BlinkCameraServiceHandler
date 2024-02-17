namespace BlinkCommon.Interfaces.Auth;

public interface IAuthAuthVerification
{
    IVerifyEmail Email { get; }
    IVerifyPhone Phone { get; }
}