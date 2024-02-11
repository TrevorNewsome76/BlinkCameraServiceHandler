using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class AuthenticationExtension
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

    /// <summary>
    /// Extract the username and password from a string array
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns>two parameters which include the username and password</returns>
    /// <exception cref="Exception"></exception>
    public static string[] ExtractUsernameAndPassword(this string[]? arguments)
    {
        if (arguments is null)
            throw new ArgumentException(
                "Username and/or password not supplied. Use /u<username> and /p<password> after the login command.");

        var username = string.Empty;
        var password = string.Empty;

        foreach (var argument in arguments)
        {
            if (argument.StartsWith('u') && argument.Length>1)
                username = argument.Substring(1);

            if (argument.StartsWith('p') && argument.Length > 1)
                password = argument.Substring(1);
        }

        return
        [
            new(username),
            new(password),
        ];
    }

    /// <summary>
    /// Extract the pin code from a string array
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string ExtractPinCode(this string[]? arguments)
    {
        if (arguments is null)
            throw new ArgumentException(
                "Pin code not supplied. Use /v<pincode> Verify command.");

        var pinCode = string.Empty;

        foreach (var argument in arguments)
        {
            if (argument.StartsWith('v') && argument.Length > 1)
                pinCode = argument.Substring(1);
        }

        return pinCode;
    }
}