using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class SessionDetailsExtension
{
    public static ISessionDetails Initialize(this ISessionDetails sessionDetails) =>
        Duck.Implement<ISessionDetails>(new
        {
            // Auth
            Account = Duck.Implement<IAuthAccount>(new()),
            Auth = Duck.Implement<IAuth>(new()),
            Phone = Duck.Implement<IAuthPhone>(new()),
            Verification = Duck.Implement<IAuthAuthVerification>(new()),
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 0,
            LoggedInStatus = false,

            // System
            Networks = Duck.Implement<IHomeScreenNetwork[]>(new()),
            SyncModules = Duck.Implement<IHomeScreenSyncModule[]>(new()),
            Cameras = Duck.Implement<IHomeScreenCamera[]>(new()),
            Doorbells = Duck.Implement<IHomeScreenDoorbells>(new ()),
        });
}