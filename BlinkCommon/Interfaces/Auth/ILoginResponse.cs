namespace BlinkCommon.Interfaces.Auth;

public interface ILoginResponse
{
    IAuthAccount? Account { get; set; }
    IAuth? Auth { get; }
    IAuthPhone? Phone { get; }
    IAuthAuthVerification? Verification { get; }
    int Lockout_Time_Remaining { get; }
    bool Force_Password_Reset { get; }
    int Allow_Pin_Resend_Seconds { get; }
    string? Message { get; }
    int? Code { get; }
}