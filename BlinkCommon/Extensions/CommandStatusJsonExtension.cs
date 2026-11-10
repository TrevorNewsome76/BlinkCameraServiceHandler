using System.Text.Json;
using BlinkCommon.Interfaces.System;
using Shadow.Quack;

namespace BlinkCommon.Extensions;

public static class CommandStatusJsonExtension
{
    public static ICommandStatusResponse ParseCommandStatus(this string? json)
    {
        if (string.IsNullOrEmpty(json))
            return Duck.Implement<ICommandStatusResponse>(new { });

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return Duck.Implement<ICommandStatusResponse>(new
            {
                Id = ReadLong(root, "id"),
                Network_Id = ReadLong(root, "network_id"),
                Complete = root.TryGetProperty("complete", out var complete) && complete.ValueKind == JsonValueKind.True,
                Status_Code = ReadInt(root, "status_code"),
            });
        }
        catch
        {
            return Duck.Implement<ICommandStatusResponse>(new { });
        }
    }

    private static long ReadLong(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;
}
