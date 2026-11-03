using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Extensions;

public static class LocalStorageExtension
{
    public static string ToLocalClipsListString(this List<ILocalStorageClip> clips, string syncModuleName)
    {
        if (clips.Count == 0)
            return $"No clips on '{syncModuleName}''s SD card.";

        return string.Join(Environment.NewLine, clips.Select((clip, index) =>
            $"[{index + 1}] Id {clip.Id}  Camera: {clip.Camera_Name}  Recorded: {clip.Created_At:yyyy-MM-dd HH:mm:ss} UTC  Size: {clip.Size} bytes"));
    }
}
