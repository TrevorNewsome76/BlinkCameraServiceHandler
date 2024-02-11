using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class SessionDetailsExtension
{
    public static ISessionDetails Initialize(this ISessionDetails sessionDetails) =>
        Duck.Implement<ISessionDetails>(new
        {
            Account = Duck.Implement<IAccount>(new()),
            Auth = Duck.Implement<IAuth>(new()),
            Phone = Duck.Implement<IPhone>(new()),
            Verification = Duck.Implement<IVerification>(new()),
            Force_Password_Reset = false,
            Allow_Pin_Resend_Seconds = 0,
            LoggedInStatus = false,
        });
}