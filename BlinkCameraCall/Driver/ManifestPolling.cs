using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Driver;

public static class ManifestPolling
{
    public static ILocalStorageManifestResult? WaitForManifestReady(
        IApiMethods api,
        string manifestStatusUrl,
        TimeSpan? pollInterval = null,
        int maxRetries = 120)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var result = (api.Get(manifestStatusUrl) ?? string.Empty).Deserialize<ILocalStorageManifestResult>();

            if (result.Clips is not null)
                return result;

            Thread.Sleep(interval);
        }

        return null;
    }
}
