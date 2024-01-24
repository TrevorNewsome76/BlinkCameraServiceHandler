using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall
{
    internal class Program
    {
        private static ILoginResponse loginDetails;

        static void Main(string[] args)
        {
            Console.WriteLine("Blink Camera Terminal");

            // TODO: Remove hard coded file path
            var settings = File.ReadAllText(@"F:\repo\BlinkCameraServiceHandler\settings.json").Deserialize<IBlinkSettings>();

            int left = Console.CursorLeft;
            int top = Console.CursorTop;
            Console.SetCursorPosition(0, 1);

            BlinkAdapter.Initialize(settings);

            bool quitNow = false;
            while (!quitNow)
            {
                var command = Console.ReadLine() ?? string.Empty;
                var splitString = command.Split(' ');
                switch (splitString[0]?.ToLower() ?? string.Empty)
                {
                    case "login":
                        loginDetails = new BlinkApiTransactions(settings).AuthLogin() ?? Duck.Implement<ILoginResponse>(new());
                        Console.WriteLine(loginDetails?.Auth?.Token != null ? "Successfully logged in." : "FAILED to login.");
                        break;

                    case "logout":
                        ILogoutResponse? logoutResult = null;
                        if (loginDetails?.Account?.Account_Id != null && loginDetails?.Account?.Client_Id !=null)
                            logoutResult = new BlinkApiTransactions(settings).AuthLogout(loginDetails.Account.Account_Id.ToString(), loginDetails.Account.Client_Id.ToString()) ?? Duck.Implement<ILogoutResponse>(new());
                        Console.WriteLine(logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.");
                        break;
                    case "pin":
                        var result = new BlinkApiTransactions(settings).AuthVerifyPin(splitString[1], loginDetails.Account.Tier, loginDetails.Account.Account_Id, loginDetails.Account.Client_Id, loginDetails.Auth?.Token ?? string.Empty);
                        Console.WriteLine(result?.Message != null ? "(" + result.Code.ToString() + ") " + result.Message: "Unknown");
                        break;
                    case "quit": 
                    case "exit":
                        Console.WriteLine("Goodbye");
                        quitNow = true;
                        break;

                    case "help":
                        Console.WriteLine("LOGIN        Logs into Blink account.");
                        Console.WriteLine("QUIT, EXIT   Exits program.");
                        Console.WriteLine("HELP         Provides Help information for Windows commands.");
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
