using BlinkCameraCall.Extensions;
using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Dependency;

using Shadow.Quack;

namespace BlinkCameraCall
{
    internal class Program
    {

        static void Main()
        {
            Console.WriteLine("Blink Camera Terminal");

            // TODO: Remove hard coded file path. Also what happens if file does not exist. Maybe when login prompt used it will create a settings file

            var settings = Duck.Implement<IBlinkSettings>(new());
            settings.Load($"{AppContext.BaseDirectory}");

            Console.SetCursorPosition(0, 1);

            BlinkAdapter.Initialize(settings);

            bool quitNow = false;
            while (!quitNow)
            {
                var command = Console.ReadLine() ?? string.Empty;
                var parameters = command.Split(' ');
                switch (parameters[0]?.ToLower() ?? string.Empty)
                {
                    case "login":
                        Console.WriteLine(BlinkAdapter.Login(parameters) + $" ({settings.Email})");
                        break;

                    case "logout":
                        Console.WriteLine(BlinkAdapter.Logout());
                        break;
                        
                    case "pin":
                        Console.WriteLine(BlinkAdapter.VerifyPin(parameters));
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
