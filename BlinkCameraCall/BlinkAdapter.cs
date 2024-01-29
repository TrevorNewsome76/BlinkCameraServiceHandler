using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using Dependency;

using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkAdapter
{
    private static ILoginResponse _loginDetails = null!;
    private static IBlinkApiTransactions _blinkAdapter =>
        Shelf.RetrieveInstance<IBlinkApiTransactions>()
        ?? Duck.Implement<IBlinkApiTransactions>();

    public static void Initialize(IBlinkSettings configuration)
    {
        Shelf.Clear();
        Shelf.ShelveInstance(BlinkApiInterface.Initialize(configuration));
        Shelf.ShelveInstance(ApiDriver.HttpClientApiHandler());
    }

    public static string Login(string[] parameters)
    {
        // TODO: Add username and password option for someone who is logging in for the first time
        _loginDetails = _blinkAdapter?.AuthLogin()?? Duck.Implement<ILoginResponse>(new());
        _blinkAdapter?.SetAccessToken(_loginDetails?.Auth?.Token ?? string.Empty);
        return _loginDetails?.Auth?.Token != null
                                ? "Successfully logged in."
                                : "FAILED to login.";
    }

    public static string Logout()
    {
        if (_loginDetails is null)
        {
            return "Not currently logged in so Logout command was ignored.";
        }

        if (_loginDetails.Account is null)
        {
            return "Account details are missing so Logout command was ignored.";
        }

        if (_loginDetails.Account.Account_Id == 0 || _loginDetails.Account.Client_Id == 0)
        {
            return "Account Id or Client Id are missing so Logout command was ignored.";
        }

        var logoutResult = _blinkAdapter?.AuthLogout(
                                   _loginDetails.Account.Account_Id.ToString(),
                                   _loginDetails.Account.Client_Id.ToString()
                               )
                           ?? Duck.Implement<ILogoutResponse>(new());

        return logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.";
    }

    public static string VerifyPin(string[] parameters) {
        var result = _blinkAdapter?.AuthVerifyPin(
                                parameters[1],
                                _loginDetails?.Account?.Tier ?? string.Empty,
                                _loginDetails?.Account?.Account_Id ?? 0,
                                _loginDetails?.Account?.Client_Id ?? 0,
                                _loginDetails?.Auth?.Token ?? string.Empty
                                );

        return result?.Message != null ? "(" + result.Code.ToString() + ") " + result.Message : "Unknown";
    }
}