using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Extensions;

public static class CloudMediaExtension
{
    public static int ExtractHours(this string[]? arguments)
    {
        if (arguments is null)
            return 24;

        foreach (var argument in arguments)
            if (argument.StartsWith('h') && argument.Length > 1 && int.TryParse(argument.Substring(1), out var hours))
                return hours;

        return 24;
    }

    public static long ExtractMediaId(this string[]? arguments)
    {
        if (arguments is not null)
            foreach (var argument in arguments)
                if (argument.StartsWith('i') && argument.Length > 1 && long.TryParse(argument.Substring(1), out var id))
                    return id;

        throw new ArgumentException("Media id not supplied. Use -i<id>.");
    }

    public static string ToCloudMediaListString(this List<ICloudMediaItem> items, int hours)
    {
        if (items.Count == 0)
            return $"No cloud video events in the last {hours} hours.";

        return string.Join(Environment.NewLine, items.Select((item, index) =>
            $"[{index + 1}] Id {item.Id}  Device: {item.Device_Name}  Recorded: {item.Created_At:yyyy-MM-dd HH:mm:ss} UTC"));
    }
}
