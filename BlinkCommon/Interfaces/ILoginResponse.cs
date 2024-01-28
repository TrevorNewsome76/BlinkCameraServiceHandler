namespace BlinkCommon.Interfaces;

public interface ILoginResponse
{
    IAccount? Account { get; set; }
    IAuth? Auth { get; }
    IPhone? Phone { get; }
    IVerification? Verification { get; }
    int Lockout_Time_Remaining { get; }
    bool Force_Password_Reset { get; }
    int Allow_Pin_Resend_Seconds { get; }
    string? Message { get; }
    int? Code { get; }
}