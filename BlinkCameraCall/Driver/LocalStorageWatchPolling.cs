using System.Diagnostics;
using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Driver;

public static class LocalStorageWatchPolling
{
    public static void Run(
        IAdapter adapter,
        IAuthAccount account,
        IReadOnlyList<IHomeScreenSyncModule> syncModules,
        Action<ILocalStorageClip, string> onNewClip,
        Action<Exception, string> onError,
        Action<TimeSpan, TimeSpan, TimeSpan> onIntervalTooShort,
        Action<TimeSpan> onIntervalPersisted,
        AdaptivePollInterval interval,
        CancellationToken token,
        int maxIterations = int.MaxValue)
    {
        if (syncModules.Count == 0)
            return;

        var seenClipIdsBySyncModule = new Dictionary<long, HashSet<long>>();

        for (var iteration = 0; iteration < maxIterations && !token.IsCancellationRequested; iteration++)
        {
            var roundStopwatch = Stopwatch.StartNew();
            var slowestModuleElapsed = TimeSpan.Zero;
            var cancelledMidRound = false;

            foreach (var syncModule in syncModules)
            {
                if (token.IsCancellationRequested)
                {
                    cancelledMidRound = true;
                    break;
                }

                var moduleStopwatch = Stopwatch.StartNew();
                try
                {
                    var networkId = long.Parse(syncModule.Network_Id);
                    var requestResult = adapter.RequestLocalStorageManifest(account, networkId, syncModule.Id);
                    var manifestResult = adapter.GetLocalStorageManifest(account, networkId, syncModule.Id, requestResult!.Id, cancellationToken: token);

                    var clips = manifestResult?.Clips ?? new List<ILocalStorageClip>();
                    if (!seenClipIdsBySyncModule.TryGetValue(syncModule.Id, out var seenClipIds))
                        seenClipIdsBySyncModule[syncModule.Id] = seenClipIds = new HashSet<long>();

                    foreach (var clip in clips)
                    {
                        if (seenClipIds.Add(clip.Id))
                            onNewClip(clip, syncModule.Name);
                    }
                }
                catch (OperationCanceledException)
                {
                    // A real cancel mid-poll stops the whole round and the whole loop
                    // immediately — don't swallow it as a per-module failure.
                    throw;
                }
                catch (Exception ex)
                {
                    onError(ex, syncModule.Name);
                }
                finally
                {
                    moduleStopwatch.Stop();
                    if (moduleStopwatch.Elapsed > slowestModuleElapsed)
                        slowestModuleElapsed = moduleStopwatch.Elapsed;
                }
            }

            roundStopwatch.Stop();

            if (cancelledMidRound || token.IsCancellationRequested)
                break;

            var newInterval = interval.ReportRound(roundStopwatch.Elapsed, slowestModuleElapsed);
            if (newInterval.HasValue)
            {
                onIntervalTooShort(roundStopwatch.Elapsed, slowestModuleElapsed, newInterval.Value);
                onIntervalPersisted(newInterval.Value);
            }

            if (token.WaitHandle.WaitOne(interval.Current))
                break;
        }
    }
}
