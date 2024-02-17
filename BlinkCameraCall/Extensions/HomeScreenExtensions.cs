namespace BlinkCameraCall.Extensions;

public static class HomeScreenExtensions
{
    public static string ExtractHomeScreenGetCommand(this string[] arguments)
    {
        var getCommand = string.Empty;

        foreach (var argument in arguments)
            if (argument.StartsWith('i') && argument.Length > 1)
                getCommand = argument.Substring(1);

        return string.IsNullOrEmpty(getCommand) 
            ? "Null or empty command. Cannot process nothing." 
            : getCommand;
    }
}