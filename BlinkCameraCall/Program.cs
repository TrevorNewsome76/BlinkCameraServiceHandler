using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall
{
    internal class Program
    {
        private static ILoginResponse LoginDetails;

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
                var command = Console.ReadLine();
                switch (command?.ToLower() ?? string.Empty)
                {
                    case "login":
                        LoginDetails = new BlinkApiTransactions(settings).AuthLogin() ?? Duck.Implement<ILoginResponse>(new());
                        Console.WriteLine(LoginDetails?.Auth?.Token != null ? "Successfully logged in." : "FAILED to login.");
                        break;

                    case "logout":
                        ILogoutResponse? logoutResult = null;
                        if (LoginDetails?.Account?.Account_Id != null && LoginDetails?.Account?.Client_Id !=null)
                            logoutResult = new BlinkApiTransactions(settings).AuthLogout(LoginDetails.Account.Account_Id.ToString(), LoginDetails.Account.Client_Id.ToString()) ?? Duck.Implement<ILogoutResponse>(new());
                        Console.WriteLine(logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.");
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
