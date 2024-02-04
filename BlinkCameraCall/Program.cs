using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;

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

            
            var blinkLibrary = new BlinkLibrary(new BlinkAdapter(settings));

            bool quitNow = false;
            while (!quitNow)
            {
                var command = Console.ReadLine() ?? string.Empty;
                var arguments = command.Split(' ');
                switch (arguments[0]?.ToLower() ?? string.Empty)
                {
                    case "login":
                        Console.WriteLine(blinkLibrary.Login("Test","Test"));
                        break;

                    case "logout":
                        Console.WriteLine(blinkLibrary.Logout());
                        break;
                    case "verify":
                        Console.WriteLine(blinkLibrary.VerifyPin(arguments));
                        break;
                    case "quit": 
                    case "exit":
                        Console.WriteLine("Goodbye");
                        quitNow = true;
                        break;

                    case "help":
                        Console.WriteLine(blinkLibrary.Help(arguments)); 
                        Console.WriteLine("PIN <code>       Verifies sms pin number sent after new login.");
                        Console.WriteLine("QUIT, EXIT       Exits program.");
                        Console.WriteLine("HELP             Provides Help information for Windows commands.");
                        quitNow = true;
                        break;

                    default:
                        Console.WriteLine("Unknown Command " + command);
                        break;
                }
                Console.WriteLine();
            }
        }
    }
}
