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
        int maxRetries = 120,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = (api.Get(manifestStatusUrl) ?? string.Empty).Deserialize<ILocalStorageManifestResult>();

            if (result.Clips is not null)
                return result;

            if (cancellationToken.WaitHandle.WaitOne(interval))
                cancellationToken.ThrowIfCancellationRequested();
        }

        return null;
    }
}
