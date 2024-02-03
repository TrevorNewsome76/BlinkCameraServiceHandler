using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;

using Shadow.Quack;

namespace BlinkCameraCall
{
    internal class Program
    {

        static void Main()
        {

            var sessionDetails = Duck.Implement<ISessionDetails>(new()).Initialize();

            Console.WriteLine("Blink Camera Terminal");

            // TODO: Remove hard coded file path. Also what happens if file does not exist. Maybe when login prompt used it will create a settings file

            var settings = Duck.Implement<IBlinkSettings>(new());
            settings.Load($"{AppContext.BaseDirectory}");

            Console.SetCursorPosition(0, 1);

            var blinkAdapter = new BlinkAdapter(settings);

            bool quitNow = false;
            while (!quitNow)
            {
                var command = Console.ReadLine() ?? string.Empty;
                var parameters = command.Split(' ');
                switch (parameters[0]?.ToLower() ?? string.Empty)
                {
                    case "login":
                        var loginResponse = blinkAdapter.Login(parameters);
                        sessionDetails = loginResponse.ConvertToSessionDetails();

                        if (sessionDetails.LoggedInStatus)
                        {
                            blinkAdapter.SetAccessToken(sessionDetails.Auth?.Token ?? string.Empty);
                            Console.WriteLine("Login to the Blink Service successful.");
                        }
                        else
                            Console.WriteLine($"Login to the Blink Service failed: {loginResponse.Message}");
                        break;

                    case "logout":
                        if (sessionDetails?.Account is not null && sessionDetails.LoggedInStatus)
                        {
                            sessionDetails.LoggedInStatus = false;
                            ILogoutResponse logoutResult = blinkAdapter.Logout(sessionDetails.Account);
                            Console.WriteLine(logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.");
                        }
                        else
                        {
                            Console.WriteLine($"Not currently logged into Blink service.");
                        }
                        break;
                        
                    case "pin":
                        if (sessionDetails?.Account is not null && sessionDetails.LoggedInStatus)
                        {
                            var setPinResponse = blinkAdapter.VerifyPin(sessionDetails.Account, parameters[1]);
                            Console.WriteLine(setPinResponse.Message);
                        }

                        break;
                    case "quit": 
                    case "exit":
                        Console.WriteLine("Goodbye");
                        quitNow = true;
                        break;

                    case "help":
                        Console.WriteLine("LOGIN            Logs into Blink account (using settings file."); 
                        Console.WriteLine("PIN <code>       Verifies sms pin number sent after new login.");
                        Console.WriteLine("QUIT, EXIT       Exits program.");
                        Console.WriteLine("HELP             Provides Help information for Windows commands.");
                        quitNow = true;
                        break;

                    default:
                        Console.WriteLine("Unknown Command " + command);
                        break;
                }
            }
        }
    }
}
