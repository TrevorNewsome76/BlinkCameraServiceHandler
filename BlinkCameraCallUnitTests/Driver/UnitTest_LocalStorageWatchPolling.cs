using System.Diagnostics;
using BlinkCameraCall;
using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Dependency;
using FluentAssertions;
using Shadow.Quack;
using Xunit;

namespace BlinkCameraCallUnitTests.Driver;

[Collection("Sequential")]
public class UnitTest_LocalStorageWatchPolling
{
    // Built just for this file: unlike UnitTest_EventWatchPolling's FakeApiTransactions (a flat
    // response queue), this returns controllable, per-sync-module manifests so each module in a
    // round can behave independently (new clips, throwing, or never completing).
    private class FakeApiTransactions(
        Dictionary<long, Func<IManifestRequestResponse?>> requestResponses,
        Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>> manifestResponses) : IApiTransactions
    {
        public bool SetAccessToken(string accessToken) => true;
        public ILoginResponse? AuthLogin() => null;
        public ILoginResponse? AuthLogin(string username, string password) => null;
        public ILoginResponse? AuthCompleteTwoFactorLogin(string code) => null;
        public ILogoutResponse? AuthLogout(IAuthAccount account) => null;
        public IVerifyPinResponse? AuthVerifyPin(IAuthAccount account, string pinCode) => null;
        public IGetHomeScreenResponse? SystemGetHomeScreen(IAuthAccount account) => null;
        public byte[]? DownloadBytes(string url) => null;
        public ICommandStatusResponse? SystemTriggerCameraSnapshot(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemTriggerDoorbellSnapshot(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemEnableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public ICommandStatusResponse? SystemDisableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public ICommandStatusResponse? SystemEnableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public ICommandStatusResponse? SystemDisableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public ICommandStatusResponse? SystemArmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public ICommandStatusResponse? SystemDisarmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public IMediaChangedResponse? SystemGetRecentMedia(IAuthAccount account, DateTime sinceUtc) => null;
        public string BuildLocalStorageClipUrl(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId) => string.Empty;
        public ICommandStatusResponse? SystemPrepareLocalStorageClip(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;

        public IManifestRequestResponse? SystemRequestLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId) =>
            requestResponses.TryGetValue(syncModuleId, out var fn)
                ? fn()
                : Duck.Implement<IManifestRequestResponse>(new { Id = syncModuleId });

        public ILocalStorageManifestResult? SystemGetLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId, long manifestRequestId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) =>
            manifestResponses.TryGetValue(syncModuleId, out var fn)
                ? fn(cancellationToken)
                : null;
    }

    private static readonly IAdapter DummyAdapter = Duck.Implement<IAdapter>();
    private static readonly IAuthAccount DummyAccount = Duck.Implement<IAuthAccount>(new());

    private static IHomeScreenSyncModule SyncModule(long id, string name) =>
        Duck.Implement<IHomeScreenSyncModule>(new { Id = id, Name = name, Network_Id = "1" });

    private static ILocalStorageClip Clip(long id, string cameraName) =>
        Duck.Implement<ILocalStorageClip>(new
        {
            Id = id,
            Camera_Name = cameraName,
            Created_At = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Size = 12345L,
        });

    private static ILocalStorageManifestResult Manifest(params ILocalStorageClip[] clips) =>
        Duck.Implement<ILocalStorageManifestResult>(new
        {
            Manifest_Id = 1L,
            Clips = clips.ToList(),
        });

    private static readonly Action<TimeSpan, TimeSpan, TimeSpan> NoOpOnIntervalTooShort = (_, _, _) => { };
    private static readonly Action<TimeSpan> NoOpOnIntervalPersisted = _ => { };

    [Fact]
    public void Test_Run_MultipleModules_DetectsNewClipsOnEachInListOrder()
    {
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "Garage");
        var moduleB = SyncModule(2, "Pergoda");
        var clipA = Clip(101, "Garage");
        var clipB = Clip(201, "Pergoda");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => Manifest(clipA),
                [2] = _ => Manifest(clipB),
            }));
        var reported = new List<(long ClipId, string ModuleName)>();

        //act
        LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { moduleA, moduleB },
            (clip, name) => reported.Add((clip.Id, name)),
            (ex, name) => { },
            NoOpOnIntervalTooShort,
            NoOpOnIntervalPersisted,
            new AdaptivePollInterval(TimeSpan.Zero),
            CancellationToken.None,
            maxIterations: 1);

        //assert
        reported.Should().Equal((101L, "Garage"), (201L, "Pergoda"));
    }

    [Fact]
    public void Test_Run_RepeatedRound_DoesNotReportSameClipTwice()
    {
        //assign
        Shelf.Clear();
        var module = SyncModule(1, "Garage");
        var clip = Clip(101, "Garage");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => Manifest(clip),
            }));
        var reported = new List<long>();

        //act
        LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { module },
            (c, name) => reported.Add(c.Id),
            (ex, name) => { },
            NoOpOnIntervalTooShort,
            NoOpOnIntervalPersisted,
            new AdaptivePollInterval(TimeSpan.Zero),
            CancellationToken.None,
            maxIterations: 2);

        //assert
        reported.Should().Equal(101L);
    }

    [Fact]
    public void Test_Run_SameClipIdOnDifferentModules_BothReported()
    {
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "Garage");
        var moduleB = SyncModule(2, "Pergoda");
        var clipOnA = Clip(999, "Garage");
        var clipOnB = Clip(999, "Pergoda");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => Manifest(clipOnA),
                [2] = _ => Manifest(clipOnB),
            }));
        var reported = new List<(long ClipId, string ModuleName)>();

        //act
        LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { moduleA, moduleB },
            (clip, name) => reported.Add((clip.Id, name)),
            (ex, name) => { },
            NoOpOnIntervalTooShort,
            NoOpOnIntervalPersisted,
            new AdaptivePollInterval(TimeSpan.Zero),
            CancellationToken.None,
            maxIterations: 1);

        //assert
        reported.Should().Equal((999L, "Garage"), (999L, "Pergoda"));
    }

    [Fact]
    public void Test_Run_OneModuleThrows_StillChecksTheRemainingModulesInThatRound()
    {
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "First");
        var moduleB = SyncModule(2, "Second");
        var moduleC = SyncModule(3, "Third");
        var clipC = Clip(301, "Third");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>
            {
                [2] = () => throw new InvalidOperationException("boom"),
            },
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => Manifest(),
                [3] = _ => Manifest(clipC),
            }));
        var reported = new List<(long ClipId, string ModuleName)>();
        var errors = new List<(string Message, string ModuleName)>();

        //act
        LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { moduleA, moduleB, moduleC },
            (clip, name) => reported.Add((clip.Id, name)),
            (ex, name) => errors.Add((ex.Message, name)),
            NoOpOnIntervalTooShort,
            NoOpOnIntervalPersisted,
            new AdaptivePollInterval(TimeSpan.Zero),
            CancellationToken.None,
            maxIterations: 1);

        //assert
        errors.Should().Equal(("boom", "Second"));
        reported.Should().Equal((301L, "Third"));
    }

    [Fact]
    public void Test_Run_CancelledMidManifestPoll_StopsImmediately()
    {
        // Proves cancellation during a module's manifest poll propagates out of Run entirely
        // (rather than being swallowed as a per-module error or merely ending the round), and
        // that it does so promptly rather than running out the retry budget. Uses CancelAfter (an
        // independent timer) so the signal genuinely arrives mid-wait, and a fake Get hook whose
        // wait loop mirrors ManifestPolling.WaitForManifestReady's own real, cancellable wait (a
        // real 5s interval) rather than a synchronous same-thread cancel, which couldn't land
        // mid-wait in a loop like this.
        //assign
        Shelf.Clear();
        var module = SyncModule(1, "Garage");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = token =>
                {
                    for (var attempt = 0; attempt < 1000; attempt++)
                    {
                        token.ThrowIfCancellationRequested();
                        if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                            token.ThrowIfCancellationRequested();
                    }
                    return null;
                },
            }));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        var tooShortCalled = false;
        var persistedCalled = false;

        //act
        var stopwatch = Stopwatch.StartNew();
        Action act = () => LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { module },
            (clip, name) => { },
            (ex, name) => { },
            (total, slowest, newInterval) => tooShortCalled = true,
            newInterval => persistedCalled = true,
            new AdaptivePollInterval(TimeSpan.FromSeconds(5)),
            cts.Token,
            maxIterations: 1000);

        //assert
        act.Should().Throw<OperationCanceledException>();
        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "cancellation mid-manifest-poll should interrupt immediately, not run out the retry budget");
        tooShortCalled.Should().BeFalse();
        persistedCalled.Should().BeFalse();
    }

    [Fact]
    public void Test_Run_RoundSlowerThanInterval_RetunesToSlowestModulePlusOneSecond()
    {
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "First");
        var moduleB = SyncModule(2, "Slow");
        var moduleC = SyncModule(3, "Third");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => Manifest(),
                [2] = _ => { Thread.Sleep(60); return Manifest(); },
                [3] = _ => Manifest(),
            }));
        var interval = new AdaptivePollInterval(TimeSpan.FromMilliseconds(10));
        var tooShortCalls = new List<(TimeSpan Total, TimeSpan Slowest, TimeSpan NewInterval)>();
        var persistedCalls = new List<TimeSpan>();

        //act
        LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { moduleA, moduleB, moduleC },
            (clip, name) => { },
            (ex, name) => { },
            (total, slowest, newInterval) => tooShortCalls.Add((total, slowest, newInterval)),
            newInterval => persistedCalls.Add(newInterval),
            interval,
            CancellationToken.None,
            maxIterations: 1);

        //assert
        tooShortCalls.Should().HaveCount(1);
        persistedCalls.Should().HaveCount(1);
        tooShortCalls[0].Total.Should().BeGreaterThanOrEqualTo(tooShortCalls[0].Slowest,
            "the round's total time sums every module's own time, so it can't be less than the slowest one");
        tooShortCalls[0].Slowest.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(60));
        tooShortCalls[0].NewInterval.Should().Be(tooShortCalls[0].Slowest + TimeSpan.FromSeconds(1));
        persistedCalls[0].Should().Be(tooShortCalls[0].NewInterval);
        interval.Current.Should().Be(tooShortCalls[0].NewInterval);
    }

    [Fact]
    public void Test_Run_EmptySyncModuleList_ReturnsImmediatelyWithoutLooping()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>()));
        var onNewClipCalled = false;
        var onErrorCalled = false;
        var onIntervalTooShortCalled = false;
        var onIntervalPersistedCalled = false;

        //act
        Action act = () => LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule>(),
            (clip, name) => onNewClipCalled = true,
            (ex, name) => onErrorCalled = true,
            (total, slowest, newInterval) => onIntervalTooShortCalled = true,
            newInterval => onIntervalPersistedCalled = true,
            new AdaptivePollInterval(TimeSpan.Zero),
            CancellationToken.None,
            maxIterations: 5);

        //assert
        act.Should().NotThrow();
        onNewClipCalled.Should().BeFalse();
        onErrorCalled.Should().BeFalse();
        onIntervalTooShortCalled.Should().BeFalse();
        onIntervalPersistedCalled.Should().BeFalse();
    }
}
