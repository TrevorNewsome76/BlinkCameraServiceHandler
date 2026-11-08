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
        "get",
        "map",
        "device",
        "snap",
        "image",
        "videos",
        "download",
        "localclips",
        "localdownload",
        "help",
        "exit",
        "cd",
        "ls",
        "trigger",
    };

    private static readonly string[] BarePositionalCommands = new[]
    {
        "cd",
        "ls",
        "trigger",
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

        var verbEnd = consoleCommand.IndexOf(' ');
        var verb = verbEnd < 0 ? consoleCommand : consoleCommand[..verbEnd];
        var remainder = verbEnd < 0 ? string.Empty : consoleCommand[(verbEnd + 1)..].Trim();

        if (BarePositionalCommands.Contains(verb.ToLower()))
        {
            return CreateBareReturnObject(verb, remainder, bareDownload: false).ValidateCommand();
        }

        if (verb.Equals("download", StringComparison.OrdinalIgnoreCase)
            && remainder.Length > 0
            && !remainder.StartsWith('-'))
        {
            return CreateBareReturnObject(verb, remainder, bareDownload: true).ValidateCommand();
        }

        return CreateReturnObject(consoleCommand.Split(" -")).ValidateCommand();
    }

    private static ICommandAndArguments CreateBareReturnObject(string command, string target, bool bareDownload) =>
        Duck.Implement<ICommandAndArguments>(new
        {
            Command = command,
            Arguments = new[] { target },
            BareDownload = bareDownload,
        });

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