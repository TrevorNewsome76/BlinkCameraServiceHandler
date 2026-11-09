using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Driver;

public static class EventWatchPolling
{
    public static void Run(
        IAdapter adapter,
        IAuthAccount account,
        Action<ICloudMediaItem> onNewEvent,
        Action<Exception> onError,
        CancellationToken token,
        TimeSpan? pollInterval = null,
        int maxIterations = int.MaxValue)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(20);
        var sinceUtc = DateTime.UtcNow;
        var seenIds = new HashSet<long>();

        for (var iteration = 0; iteration < maxIterations && !token.IsCancellationRequested; iteration++)
        {
            try
            {
                var response = adapter.GetRecentMedia(account, sinceUtc);
                var newItems = (response?.Media ?? new List<ICloudMediaItem>())
                    .Where(item => !item.Deleted && seenIds.Add(item.Id))
                    .OrderBy(item => item.Created_At);

                foreach (var item in newItems)
                    onNewEvent(item);

                sinceUtc = DateTime.UtcNow.AddSeconds(-30);
            }
            catch (Exception ex)
            {
                onError(ex);
            }

            if (token.IsCancellationRequested)
                break;

            Thread.Sleep(interval);
        }
    }
}
