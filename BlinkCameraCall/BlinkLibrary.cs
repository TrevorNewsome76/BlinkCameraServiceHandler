using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkLibrary(IBlinkSettings settings)
{
    private ISessionDetails _sessionDetails = Duck.Implement<ISessionDetails>(new()).Initialize();
    private readonly BlinkAdapter _adapter = new(settings);

    public string Login(string[] arguments)
    {
        var loginResponse = _adapter.Login(arguments);
        _sessionDetails = loginResponse.ConvertToSessionDetails();

        if (_sessionDetails.LoggedInStatus)
        {
            _adapter.SetAccessToken(_sessionDetails.Auth?.Token ?? string.Empty);
            return "Login to the Blink Service successful.";
        }
        else
            return $"Login to the Blink Service failed: {loginResponse.Message}";
    }

    public string Logout()
    {
        if (_sessionDetails?.Account is not null && _sessionDetails.LoggedInStatus)
        {
            _sessionDetails.LoggedInStatus = false;
            ILogoutResponse logoutResult = _adapter.Logout(_sessionDetails.Account);
            return logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.";
        }
        else
        {
            return "Not currently logged into Blink service.";
        }
    }

    public string Verify(string[] arguments)
    {
        if (_sessionDetails?.Account is not null && _sessionDetails.LoggedInStatus)
        {
            var setPinResponse = _adapter.VerifyPin(_sessionDetails.Account, arguments[0]);
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