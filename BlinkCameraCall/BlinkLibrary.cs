﻿using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkLibrary(IAdapter adapter)
{
    private ISessionDetails _sessionDetails = Duck.Implement<ISessionDetails>(new()).Initialize();

    public string Login()
    {
        var loginResponse = adapter.Login();

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

    public string Login(string[] arguments)
    {
        var loginParameters = arguments.ExtractUsernameAndPassword();
        var loginResponse = adapter.Login(loginParameters[0], loginParameters[1]);
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
                adapter.Logout(_sessionDetails.Account ?? Duck.Implement<IAuthAccount>(new()));

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
            var pinCode = arguments.ExtractPinCode();
            var setPinResponse = adapter.VerifyPin(_sessionDetails.Account, pinCode);
            return setPinResponse.Message ?? string.Empty;
        }
        else
            return "Not currently logged into Blink service.";
    }

    public string Help(string[] arguments)
    {
        return new string("LOGIN            Logs into Blink account (using settings file." +
                          System.Environment.NewLine +
                          "VERIFY <code>       Verifies sms pin number sent after new login." +
                          System.Environment.NewLine +
                          "QUIT, EXIT       Exits program." + System.Environment.NewLine +
                          "HELP             Provides Help information for Windows commands." +
                          System.Environment.NewLine
        );
    }

    public string Get(string[] arguments)
    {
        var getParameter = arguments.ExtractHomeScreenGetCommand();

        if (_sessionDetails?.Account is not null && _sessionDetails.LoggedInStatus)
        {

            IGetHomeScreenResponse response = Duck.Implement<IGetHomeScreenResponse>(new());

            switch (getParameter.ToLower())
            {
                case "home":
                    response = adapter.GetHomeScreen(_sessionDetails.Account);
                    break;
                default: return "Unrecognised HomeSystem command";
            }
            _sessionDetails.Networks = response?.Networks;
            _sessionDetails.SyncModules = response?.Sync_Modules;
            _sessionDetails.Cameras = response?.Cameras;
            _sessionDetails.Doorbells = response?.Doorbells;

            return "Command ran successfully.";
        }
        else
        {
            return $"Not logged in to a system so this command cannot be used.";
        }
    }
}