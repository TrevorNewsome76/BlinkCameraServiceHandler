using System.ComponentModel.Design;
using BlinkCameraCall.Extensions;
using BlinkCameraCall.IO;
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
            Console.WriteLine($"Using settings file: {AppContext.BaseDirectory}config.json");

            if (string.IsNullOrEmpty(settings.HardwareId))
            {
                settings.HardwareId = Guid.NewGuid().ToString().ToUpperInvariant();
                settings.Save($"{AppContext.BaseDirectory}");
            }

            //Console.SetCursorPosition(0, 1);

            
            var blinkLibrary = new BlinkLibrary(new BlinkAdapter(settings));
            var shellLineEditor = new ShellLineEditor(new SystemConsoleIO());
            var progressSpinner = new ProgressSpinner(new SystemConsoleIO());
            var shellSession = new ShellSession(blinkLibrary);

            Action<string> alertSink = (Console.IsInputRedirected || Console.IsOutputRedirected)
                ? (msg => Console.WriteLine(msg))
                : (msg =>
                {
                    Console.Beep();
                    shellLineEditor.NotifyExternal(msg);
                });

            AttemptAutoLogin(blinkLibrary, settings, alertSink);

            bool quitNow = false;
            while (!quitNow)
            {
                try
                {
                    string? rawLine;
                    if (Console.IsInputRedirected || Console.IsOutputRedirected)
                    {
                        Console.Write(shellSession.Prompt);
                        rawLine = Console.ReadLine();

                        if (rawLine is null)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Goodbye");
                            break;
                        }
                    }
                    else
                    {
                        rawLine = shellLineEditor.ReadLine(shellSession.Prompt);
                    }

                    var command = rawLine.ProcessCommandString();

                    if (!string.IsNullOrEmpty(command.Message))
                    {
                        Console.WriteLine(command.Message);
                    }
                    else
                    {
                        switch (command.Command.ToLowerInvariant())
                        {
                            case "login":
                                Console.WriteLine(command?.Arguments is null
                                    ? blinkLibrary.Login()
                                    : blinkLibrary.Login(command.Arguments));
                                SaveCredentialsIfLoggedIn(blinkLibrary, settings);
                                TryAutoStartWatch(blinkLibrary, alertSink);
                                break;

                            case "logout":
                                Console.WriteLine(blinkLibrary.Logout());
                                break;
                            case "verify":
                                Console.WriteLine(blinkLibrary.VerifyPin(command.Arguments));
                                SaveCredentialsIfLoggedIn(blinkLibrary, settings);
                                TryAutoStartWatch(blinkLibrary, alertSink);
                                break;
                            case "quit":
                            case "exit":
                                Console.WriteLine("Goodbye");
                                quitNow = true;
                                break;
                            case "get":
                                Console.WriteLine(blinkLibrary.Get(command.Arguments));
                                break;
                            case "map":
                                Console.WriteLine(blinkLibrary.Map());
                                break;
                            case "device":
                                Console.WriteLine(blinkLibrary.Device(command.Arguments));
                                break;
                            case "snap":
                                Console.WriteLine(blinkLibrary.Snap(command.Arguments));
                                break;
                            case "image":
                                Console.WriteLine(blinkLibrary.Image(command.Arguments));
                                break;
                            case "videos":
                                Console.WriteLine(blinkLibrary.Videos(command.Arguments));
                                break;
                            case "cd":
                                Console.WriteLine(shellSession.ChangeDirectory(command.Arguments![0]));
                                break;
                            case "ls":
                                Console.WriteLine(shellSession.List());
                                break;
                            case "trigger":
                                Console.WriteLine(shellSession.Trigger());
                                break;
                            case "arm":
                                Console.WriteLine(RunWithProgress(progressSpinner, cts => shellSession.Arm(onProgress: progressSpinner.WriteLine, cancellationToken: cts.Token), () => shellSession.Arm()));
                                break;
                            case "disarm":
                                Console.WriteLine(RunWithProgress(progressSpinner, cts => shellSession.Disarm(onProgress: progressSpinner.WriteLine, cancellationToken: cts.Token), () => shellSession.Disarm()));
                                break;
                            case "watch":
                                Console.WriteLine(blinkLibrary.Watch(command.Arguments ?? Array.Empty<string>(), alertSink));
                                break;
                            case "unwatch":
                                Console.WriteLine(blinkLibrary.Unwatch());
                                break;
                            case "download":
                                Console.WriteLine(command.BareDownload
                                    ? shellSession.DownloadByName(command.Arguments![0])
                                    : blinkLibrary.Download(command.Arguments));
                                break;
                            case "localclips":
                                Console.WriteLine(blinkLibrary.LocalClips(command.Arguments));
                                break;
                            case "localdownload":
                                Console.WriteLine(blinkLibrary.LocalDownload(command.Arguments));
                                break;
                            case "help":
                                Console.WriteLine(blinkLibrary.Help(command.Arguments));
                                Console.WriteLine("LOGIN -u<username> -p<password>  Logs into system.");
                                Console.WriteLine("LOGOUT                           Logs out of the system.");
                                Console.WriteLine("GET -i<homescreen>               Gets home system information.");
                                Console.WriteLine("MAP                              Shows a tree of networks, sync modules, cameras and doorbells.");
                                Console.WriteLine("DEVICE -n<name>                  Shows detailed info for one camera/doorbell.");
                                Console.WriteLine("SNAP -n<name>                    Triggers a fresh snapshot for a camera/doorbell.");
                                Console.WriteLine("IMAGE -n<name>                   Downloads a device's current thumbnail to media/.");
                                Console.WriteLine("VIDEOS -h<hours>                 Lists recent cloud video events (default 24h).");
                                Console.WriteLine("DOWNLOAD -i<id>                  Downloads a cloud clip (id from VIDEOS) to media/.");
                                Console.WriteLine("CD <name>|..|\\                   Changes directory between hubs and devices.");
                                Console.WriteLine("LS                                Lists hubs, devices, or a device's media, depending on where you are.");
                                Console.WriteLine("TRIGGER                          Takes a fresh snapshot of the current device (cd into one first).");
                                Console.WriteLine("ARM                               Arms the current system/hub/device (cd to choose the level). Press Esc to cancel.");
                                Console.WriteLine("DISARM                            Disarms the current system/hub/device (cd to choose the level). Press Esc to cancel.");
                                Console.WriteLine("WATCH -s<seconds>                Watches for new doorbell/motion events in the background (default 20s).");
                                Console.WriteLine("UNWATCH                          Stops watching for new events.");
                                Console.WriteLine("DOWNLOAD <name>                  Downloads a media item by the name LS showed (device directory only).");
                                Console.WriteLine("LOCALCLIPS -n<syncmodule>        Lists a sync module's SD-card manifest.");
                                Console.WriteLine("LOCALDOWNLOAD -i<id> -n<sync>    Downloads an SD-card clip (id from LOCALCLIPS) to media/.");
                                Console.WriteLine("VERIFY -v<code>                     Verifies sms pin number sent after new login.");
                                Console.WriteLine("QUIT, EXIT                       Exits program.");
                                Console.WriteLine("HELP                             Provides Help information for Windows commands.");
                                break;

                            default:
                                Console.WriteLine("Unknown Command " + command);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (!string.IsNullOrEmpty(ex.InnerException?.Message))
                        Console.WriteLine(ex.InnerException.Message);
                }

                Console.WriteLine();
            }
        }

        private const string PlaceholderEmail = "test@email.com";
        private const string PlaceholderPassword = "PAssword";
        private const int MaxVerificationAttempts = 3;

        private static void TryAutoStartWatch(BlinkLibrary blinkLibrary, Action<string> alertSink)
        {
            if (blinkLibrary.IsLoggedIn)
                Console.WriteLine(blinkLibrary.Watch(Array.Empty<string>(), alertSink));
        }

        private static string RunWithProgress(ProgressSpinner progressSpinner, Func<CancellationTokenSource, string> runCancellable, Func<string> runPlain)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
                return runPlain();

            var cts = new CancellationTokenSource();
            progressSpinner.Start("Sending instructions, please wait...");

            var task = Task.Run(() => runCancellable(cts));

            while (!task.IsCompleted)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape || (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                        cts.Cancel();
                }

                progressSpinner.Tick();
                Thread.Sleep(120);
            }

            progressSpinner.Stop();
            return task.Result;
        }

        private static void AttemptAutoLogin(BlinkLibrary blinkLibrary, IBlinkSettings settings, Action<string> alertSink)
        {
            if (string.IsNullOrEmpty(settings.Email) || string.IsNullOrEmpty(settings.Password))
                return;

            if (settings.Email == PlaceholderEmail && settings.Password == PlaceholderPassword)
                return;

            Console.WriteLine("Automatically logging in...");
            var loginResult = blinkLibrary.Login();

            if (blinkLibrary.TwoFactorPending)
            {
                for (var attempt = 1; attempt <= MaxVerificationAttempts && !blinkLibrary.IsLoggedIn; attempt++)
                {
                    Console.Write("Enter the verification code Blink sent you: ");
                    var code = Console.ReadLine() ?? string.Empty;
                    Console.WriteLine(blinkLibrary.VerifyPin(new[] { $"v{code}" }));
                }
            }
            else
            {
                Console.WriteLine(loginResult);
            }

            if (!blinkLibrary.IsLoggedIn)
            {
                Console.WriteLine("Authentication failed. Check settings in config.");
                return;
            }

            SaveCredentialsIfLoggedIn(blinkLibrary, settings);
            TryAutoStartWatch(blinkLibrary, alertSink);
        }

        private static void SaveCredentialsIfLoggedIn(BlinkLibrary blinkLibrary, IBlinkSettings settings)
        {
            if (!blinkLibrary.IsLoggedIn)
                return;

            var auth = blinkLibrary.CurrentSession?.Auth;
            if (auth is null || string.IsNullOrEmpty(auth.UserName))
                return;

            settings.Email = auth.UserName;
            settings.Password = auth.Password;
            settings.Save($"{AppContext.BaseDirectory}");
        }
    }
}
