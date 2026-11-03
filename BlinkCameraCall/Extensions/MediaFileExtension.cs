using System.Text.RegularExpressions;

namespace BlinkCameraCall.Extensions;

public static class MediaFileExtension
{
    private static readonly Regex DisallowedCharacters = new("[^A-Za-z0-9 _-]", RegexOptions.Compiled);

    public static string SaveMediaFile(string deviceName, byte[] bytes, string extension, string? mediaDirectory = null)
    {
        var directory = mediaDirectory ?? Path.Combine(AppContext.BaseDirectory, "media");
        Directory.CreateDirectory(directory);

        var sanitizedName = DisallowedCharacters.Replace(deviceName, "_");
        var fileName = $"{sanitizedName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
        var path = Path.Combine(directory, fileName);

        File.WriteAllBytes(path, bytes);

        return path;
    }
}
