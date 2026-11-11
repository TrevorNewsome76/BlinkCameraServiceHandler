using System.Text.Json;
using BlinkCommon.Interfaces.System;
using Shadow.Quack;

namespace BlinkCommon.Extensions;

public static class ManifestRequestJsonExtension
{
    public static IManifestRequestResponse ParseManifestRequest(this string? json)
    {
        if (string.IsNullOrEmpty(json))
            return Duck.Implement<IManifestRequestResponse>(new { });

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return Duck.Implement<IManifestRequestResponse>(new
            {
                Id = root.TryGetProperty("id", out var value) && value.TryGetInt64(out var result) ? result : 0,
            });
        }
        catch
        {
            return Duck.Implement<IManifestRequestResponse>(new { });
        }
    }
}
