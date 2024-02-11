using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests.Extensions;

public static class CommandProcessorExtension
{

    private static readonly string[] ValidCommands = new[]
    {
        "login",
        "logout",
        "verify",
        "exit",
    };

    public static ICommandAndArguments ProcessCommandString(this string consoleCommand)
    {
        return consoleCommand.FormatCommand();
    }

    private static ICommandAndArguments FormatCommand(this string consoleCommand)
    {
        if (string.IsNullOrEmpty(consoleCommand))
        {
            return Duck.Implement<ICommandAndArguments>(
                new
                {
                    Message = "Null or empty command. Cannot process nothing.",
                    Valid = false,
                });
        }

        return CreateReturnObject(consoleCommand.Split(" -")).ValidateCommand();
    }

    private static ICommandAndArguments CreateReturnObject(string[] commandAndArguments) =>
        (commandAndArguments.Length > 1) 
            ?
            Duck.Implement<ICommandAndArguments>(new
            {
                Command = commandAndArguments[0],
                Arguments = commandAndArguments[Range.StartAt(1)],
            })
            :
            Duck.Implement<ICommandAndArguments>(new
            {
                Command = commandAndArguments[0],
            });

    private static ICommandAndArguments ValidateCommand(this ICommandAndArguments commandObject)
    {
        if (!ValidCommands.Contains(commandObject.Command.ToLower()))
        {
            commandObject.Message = "Unknown command.";
        }

        return commandObject;
    }
}