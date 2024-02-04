using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkLibrary(IAdapter adapter)
{
    private ISessionDetails _sessionDetails = Duck.Implement<ISessionDetails>(new()).Initialize();

    public string Login(string[] arguments)
    {
        var loginResponse = adapter.Login(arguments);
        _sessionDetails = loginResponse.ConvertToSessionDetails();

        if (_sessionDetails.LoggedInStatus)
        {
            adapter.SetAccessToken(_sessionDetails.Auth?.Token ?? string.Empty);
            _sessionDetails.LoggedInStatus = true;
            return "Login to the Blink Service successful.";
        }
        else
        {
            _sessionDetails.LoggedInStatus = false;
            return $"Login to the Blink Service failed: {loginResponse.Message}";
        }
    }

    public string Logout()
    {
        if (_sessionDetails.LoggedInStatus)
        {
            _sessionDetails.LoggedInStatus = false;
            ILogoutResponse logoutResult = 
                adapter.Logout(_sessionDetails.Account ?? Duck.Implement<IAccount>(new()));

            return logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.";
        }
        else
        {
            return "Not currently logged into Blink service.";
        }
    }

    public string VerifyPin(string[] arguments)
    {
        if (_sessionDetails?.Account is not null && _sessionDetails.LoggedInStatus)
        {
            var setPinResponse = adapter.VerifyPin(_sessionDetails.Account, arguments[0]);
            return setPinResponse.Message ?? string.Empty;
        }
        else
            return "Not currently logged into Blink service.";
    }

    public string Help(string[] arguments)
    {
        return new string("LOGIN            Logs into Blink account (using settings file." + System.Environment.NewLine +
                          "PIN <code>       Verifies sms pin number sent after new login." + System.Environment.NewLine +
                          "QUIT, EXIT       Exits program." + System.Environment.NewLine +
                          "HELP             Provides Help information for Windows commands." + System.Environment.NewLine
        );
    }

}