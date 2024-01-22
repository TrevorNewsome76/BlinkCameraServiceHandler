namespace BlinkCommon.Interfaces;

public interface ILoginResponse
{
    IAccount Account { get; set; }
    IAuth Auth { get; }
    IPhone Phone { get; }
    IVerification Verification { get; }
}