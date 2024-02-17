using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;

namespace BlinkCommon.Interfaces;

public interface ISessionDetails
{
    // Auth command information
    IAuthAccount? Account { get; set; }
    IAuth? Auth { get; }
    IAuthPhone? Phone { get; }
    IAuthAuthVerification? Verification { get; }
    int Lockout_Time_Remaining { get; }
    bool Force_Password_Reset { get; }
    int Allow_Pin_Resend_Seconds { get; }

    /// <summary>
    /// Check if the user has been successfully logged in.
    /// If this is true then this object should have been populated
    /// with the login result details.
    /// </summary>
    bool LoggedInStatus { get; set; }

    // System command information
    List<IHomeScreenNetwork>? Networks { get; set; }
    List<IHomeScreenSyncModule>? SyncModules { get; set; }
    List<IHomeScreenCamera>? Cameras { get; set; }
    List<IHomeScreenDoorbells>? Doorbells { get; set; }
}