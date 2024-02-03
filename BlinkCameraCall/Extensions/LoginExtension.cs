using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class LoginExtension
{
    /// <summary>
    /// Converts a Login response into a session object
    /// </summary>
    /// <param name="loginResponse">The login response made by the Login method</param>
    /// <returns>A populated Session Details object</returns>
    public static ISessionDetails ConvertToSessionDetails(this ILoginResponse? loginResponse) =>
        Duck.Implement<ISessionDetails>(new
        {
            Account = loginResponse?.Account ?? Duck.Implement<IAccount>(new()),
            Auth = loginResponse?.Auth ?? Duck.Implement<IAuth>(new()),
            Phone = loginResponse?.Phone ?? Duck.Implement<IPhone>(new()),
            Verification = loginResponse?.Verification ?? Duck.Implement<IVerification>(new()),
            Force_Password_Reset = loginResponse?.Force_Password_Reset ?? false,
            Allow_Pin_Resend_Seconds = loginResponse?.Allow_Pin_Resend_Seconds ?? 0,
            LoggedInStatus = !string.IsNullOrEmpty(loginResponse?.Auth?.Token ?? string.Empty),
        });
}