﻿using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using Shadow.Quack;

namespace BlinkCameraCall;

public static class SettingsFile
{
    public static void Load(this IBlinkSettings settings, string path)
    {
        var fileSettings = Duck.Implement<IBlinkSettings>(new
        {
            Email = "test@email.com",
            Password = "password",
            Baseurl = "https://rest-prod.immedia-semi.com"
        });

        if (File.Exists($"{path}settings.json"))
            fileSettings = File.ReadAllText($"{path}settings.json").Deserialize<IBlinkSettings>();

        settings.BaseUrl = fileSettings.BaseUrl;
        settings.Email = fileSettings.Email;
        settings.Password = fileSettings.Password;
    }

    
}