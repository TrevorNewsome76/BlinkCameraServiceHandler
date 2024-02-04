using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCallUnitTests.Extensions;

public static class CommandProcessorExtension
{
    public static ICommandAndArguments ProcessCommandString(this string consoleCommand)
    {
        return consoleCommand.FormatCommand();
    }

    private static ICommandAndArguments FormatCommand(this string consoleCommand)
    {
        if (string.IsNullOrEmpty(consoleCommand)) return Duck.Implement<ICommandAndArguments>(
            new
            {
                ErrorMessage = "Null or empty command. Cannot process nothing.",
            });

        var command = consoleCommand.ToLower();
        var commandAndArguments = command.Split(" -");

        if (commandAndArguments.Length > 1)
        {
            var arguments = commandAndArguments[Range.StartAt(1)];
            return Duck.Implement<ICommandAndArguments>(new
            {
                Command = commandAndArguments[0],
                Arguments = arguments,
            });
        }
        else
        {
            return Duck.Implement<ICommandAndArguments>(new
            {
                Command = commandAndArguments[0],
            });

        }
    }

}