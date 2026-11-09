namespace BlinkCameraCall.Extensions;

public static class WatchExtension
{
    public static int ExtractWatchIntervalSeconds(this string[]? arguments)
    {
        if (arguments is null)
            return 20;

        foreach (var argument in arguments)
            if (argument.StartsWith('s') && argument.Length > 1 && int.TryParse(argument.Substring(1), out var seconds))
                return seconds;

        return 20;
    }
}
