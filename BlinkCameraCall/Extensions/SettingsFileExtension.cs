using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall.Extensions;

public static class SettingsFileExtension
{
    public static void Load(this IBlinkSettings settings, string path)
    {
        var fileSettings = Duck.Implement<IBlinkSettings>(new
        {
            Email = "test@email.com",
            Password = "PAssword",
            Baseurl = "https://rest-prod.immedia-semi.com",
            HardwareId = string.Empty,
        });

        if (File.Exists($"{path}settings.json"))
            fileSettings = File.ReadAllText($"{path}settings.json").Deserialize<IBlinkSettings>();

        settings.BaseUrl = fileSettings.BaseUrl;
        settings.Email = fileSettings.Email;
        settings.Password = fileSettings.Password;
        settings.HardwareId = fileSettings.HardwareId ?? string.Empty;
    }

    public static void Save(this IBlinkSettings settings, string path) =>
        File.WriteAllText($"{path}settings.json", settings.Serialize());
}