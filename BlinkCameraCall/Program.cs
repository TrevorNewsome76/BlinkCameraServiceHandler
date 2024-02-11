using System.ComponentModel.Design;
using BlinkCameraCall.Extensions;
using BlinkCameraCallUnitTests.Extensions;
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
                var command = (Console.ReadLine() ?? string.Empty).ProcessCommandString();

                if (string.IsNullOrEmpty(command.Message))
                {
                    Console.WriteLine(command.Message);
                } 
                else
                {
                    switch (command.Command)
                    {
                        case "login":
                            Console.WriteLine(blinkLibrary.Login(command.Arguments));
                            break;

                        case "logout":
                            Console.WriteLine(blinkLibrary.Logout());
                            break;
                        case "verify":
                            Console.WriteLine(blinkLibrary.VerifyPin(command.Arguments));
                            break;
                        case "quit":
                        case "exit":
                            Console.WriteLine("Goodbye");
                            quitNow = true;
                            break;

                        case "help":
                            Console.WriteLine(blinkLibrary.Help(command.Arguments));
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
                
                
                Console.WriteLine();
            }
        }
    }
}
