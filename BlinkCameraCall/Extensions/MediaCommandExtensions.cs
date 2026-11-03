namespace BlinkCameraCall.Extensions;

public static class MediaCommandExtensions
{
    public static string ExtractDeviceName(this string[]? arguments)
    {
        if (arguments is null)
            throw new ArgumentException("Device name not supplied. Use -n<name>.");

        var name = string.Empty;

        foreach (var argument in arguments)
            if (argument.StartsWith('n') && argument.Length > 1)
                name = argument.Substring(1);

        return string.IsNullOrEmpty(name)
            ? throw new ArgumentException("Device name not supplied. Use -n<name>.")
            : name;
    }
}
