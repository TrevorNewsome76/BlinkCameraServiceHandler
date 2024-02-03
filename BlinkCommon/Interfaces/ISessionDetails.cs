namespace BlinkCommon.Interfaces;

public interface ISessionDetails
{
    IAccount? Account { get; set; }
    IAuth? Auth { get; }
    IPhone? Phone { get; }
    IVerification? Verification { get; }
    int Lockout_Time_Remaining { get; }
    bool Force_Password_Reset { get; }
    int Allow_Pin_Resend_Seconds { get; }

    /// <summary>
    /// Check if the user has been successfully logged in.
    /// If this is true then this object should have been populated
    /// with the login result details.
    /// </summary>
    bool LoggedInStatus { get; set; }
}