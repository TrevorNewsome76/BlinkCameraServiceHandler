using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Dependency;

using Shadow.Quack;

namespace BlinkCameraCall
{
    internal class Program
    {
        private static ILoginResponse _loginDetails = null!;
        private static IBlinkApiTransactions BlinkAdapter => 
            Shelf.RetrieveInstance<IBlinkApiTransactions>() 
            ?? Duck.Implement<IBlinkApiTransactions>();

        static void Main()
        {
            Console.WriteLine("Blink Camera Terminal");

            // TODO: Remove hard coded file path. Also what happens if file does not exist. Maybe when login prompt used it will create a settings file
            var settings = File.ReadAllText(@"F:\repo\BlinkCameraServiceHandler\settings.json").Deserialize<IBlinkSettings>();

            Console.SetCursorPosition(0, 1);

            BlinkCameraCall.BlinkAdapter.Initialize(settings);

            bool quitNow = false;
            while (!quitNow)
            {
                var command = Console.ReadLine() ?? string.Empty;
                var splitString = command.Split(' ');
                switch (splitString[0]?.ToLower() ?? string.Empty)
                {
                    case "login":
                        _loginDetails = BlinkAdapter?.AuthLogin() 
                                        ?? Duck.Implement<ILoginResponse>(new());

                        BlinkAdapter?.SetAccessToken(_loginDetails?.Auth?.Token ?? string.Empty);
                        Console.WriteLine(_loginDetails?.Auth?.Token != null 
                            ? "Successfully logged in." 
                            : "FAILED to login.");

                        break;

                    case "logout":

                        if (_loginDetails is null)
                        {
                            Console.WriteLine("Not currently logged in so Logout command was ignored.");
                            break;
                        }

                        if (_loginDetails.Account is null)
                        {
                            Console.WriteLine("Account details are missing so Logout command was ignored.");
                            break;
                        }

                        if (_loginDetails.Account.Account_Id == 0 || _loginDetails.Account.Client_Id == 0)
                        {
                            Console.WriteLine("Account Id or Client Id are missing so Logout command was ignored.");
                            break;
                        }
                        
                        var logoutResult = BlinkAdapter?.AuthLogout(
                                                   _loginDetails.Account.Account_Id.ToString(), 
                                                   _loginDetails.Account.Client_Id.ToString()
                                               ) 
                                           ?? Duck.Implement<ILogoutResponse>(new());

                        Console.WriteLine(logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.");
                        break;

                    case "pin":
                        var result = BlinkAdapter?.AuthVerifyPin(
                                splitString[1], 
                                _loginDetails?.Account?.Tier ?? string.Empty, 
                                _loginDetails?.Account?.Account_Id ?? 0, 
                                _loginDetails?.Account?.Client_Id ?? 0, 
                                _loginDetails?.Auth?.Token ?? string.Empty
                                );

                        Console.WriteLine(result?.Message != null ? "(" + result.Code.ToString() + ") " + result.Message: "Unknown");
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
