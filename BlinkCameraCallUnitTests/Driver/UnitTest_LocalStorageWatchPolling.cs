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
        // Round 1 establishes each module's baseline (Fix 2: a module's first successful check
        // never alerts on clips already present) — clipA/clipB are pre-existing and must NOT be
        // reported. Round 2 introduces one additional, genuinely new clip per module; those are
        // the ones that must be reported, in list order.
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "Garage");
        var moduleB = SyncModule(2, "Pergoda");
        var clipA = Clip(101, "Garage");
        var clipB = Clip(201, "Pergoda");
        var newClipA = Clip(102, "Garage");
        var newClipB = Clip(202, "Pergoda");
        var callCountA = 0;
        var callCountB = 0;
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => ++callCountA == 1 ? Manifest(clipA) : Manifest(clipA, newClipA),
                [2] = _ => ++callCountB == 1 ? Manifest(clipB) : Manifest(clipB, newClipB),
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
            maxIterations: 2);

        //assert
        reported.Should().Equal((102L, "Garage"), (202L, "Pergoda"));
    }

    [Fact]
    public void Test_Run_RepeatedRound_DoesNotReportSameClipTwice()
    {
        // Round 1 is an empty baseline round (nothing to seed). The clip first appears on round 2
        // (genuinely new — reported once). Round 3 re-returns the same clip to prove a clip, once
        // reported, isn't reported again on a later round either.
        //assign
        Shelf.Clear();
        var module = SyncModule(1, "Garage");
        var clip = Clip(101, "Garage");
        var callCount = 0;
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => ++callCount == 1 ? Manifest() : Manifest(clip),
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
            maxIterations: 3);

        //assert
        reported.Should().Equal(101L);
    }

    [Fact]
    public void Test_Run_SameClipIdOnDifferentModules_TrackedIndependentlyPerModule()
    {
        // Proves the per-module seen-clip HashSets are tracked independently, in a way that
        // isn't just an artifact of the new baseline-seeding behavior (Fix 2). Round 1 establishes
        // each module's baseline with the SAME pre-existing clip id (999) on both — under the
        // fix, that correctly never fires for either module. Round 2 re-returns the identical
        // manifests to prove that silence is stable, not a one-off. Round 3 then introduces a
        // brand-new clip id (888) that's also shared across both modules; both modules must
        // independently report it. If the two modules' seen-sets were ever accidentally shared,
        // the second module's "888" would be wrongly suppressed as already-seen the instant the
        // first module added it.
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "Garage");
        var moduleB = SyncModule(2, "Pergoda");
        var baselineClipA = Clip(999, "Garage");
        var baselineClipB = Clip(999, "Pergoda");
        var newSharedClipA = Clip(888, "Garage");
        var newSharedClipB = Clip(888, "Pergoda");
        var callCountA = 0;
        var callCountB = 0;
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => ++callCountA < 3 ? Manifest(baselineClipA) : Manifest(baselineClipA, newSharedClipA),
                [2] = _ => ++callCountB < 3 ? Manifest(baselineClipB) : Manifest(baselineClipB, newSharedClipB),
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
            maxIterations: 3);

        //assert
        reported.Should().Equal((888L, "Garage"), (888L, "Pergoda"));
    }

    [Fact]
    public void Test_Run_OneModuleThrows_StillChecksTheRemainingModulesInThatRound()
    {
        // Module "Second" throws on every round (proving modules A and C still get checked every
        // round regardless, across 2 rounds this time). Module C's round-1 clip (301) establishes
        // its baseline (Fix 2: never alerted on); round 2 introduces a genuinely new clip (302) on
        // module C, proving it's still being correctly checked and tracked after the fix.
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "First");
        var moduleB = SyncModule(2, "Second");
        var moduleC = SyncModule(3, "Third");
        var clipC = Clip(301, "Third");
        var newClipC = Clip(302, "Third");
        var callCountC = 0;
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>
            {
                [2] = () => throw new InvalidOperationException("boom"),
            },
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => Manifest(),
                [3] = _ => ++callCountC == 1 ? Manifest(clipC) : Manifest(clipC, newClipC),
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
            maxIterations: 2);

        //assert
        errors.Should().Equal(("boom", "Second"), ("boom", "Second"));
        reported.Should().Equal((302L, "Third"));
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
    public void Test_Run_MultiModuleRetuneRepeatsAcrossRounds_OnlyReportsOnce()
    {
        // End-to-end proof (through the real Run() caller, not just AdaptivePollInterval directly)
        // that once a round retunes the interval, later rounds whose own check time stays small
        // relative to the now-larger interval correctly stay silent across repeated iterations —
        // i.e. Run() doesn't re-invoke the retune callbacks merely because maxIterations > 1.
        //
        // Note: this does NOT (and provably cannot, via real Stopwatch-measured delays) exercise
        // Fix 1's new "candidate == Current" branch specifically — empirically verified: two
        // independent ~1.1s module sleeps measured a few ms apart every round (observed jitter
        // ~10-15ms), so the recomputed candidate never ties the prior one exactly, even with the
        // fix applied, firing a new (genuinely different, correctly-not-suppressed) value every
        // round instead. That exact branch is covered deterministically by
        // Test_ReportRound_RepeatedCallWithSameComputedCandidate_ReturnsNullAfterFirstRetune in
        // UnitTest_AdaptivePollInterval, which feeds ReportRound identical hardcoded values with
        // no timing involved. This test instead covers the adjacent, previously-untested
        // integration gap: multiple rounds, multiple modules, one retune, through Run() itself.
        //assign
        Shelf.Clear();
        var moduleA = SyncModule(1, "First");
        var moduleB = SyncModule(2, "Second");
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            new Dictionary<long, Func<IManifestRequestResponse?>>(),
            new Dictionary<long, Func<CancellationToken, ILocalStorageManifestResult?>>
            {
                [1] = _ => { Thread.Sleep(3); return Manifest(); },
                [2] = _ => { Thread.Sleep(3); return Manifest(); },
            }));
        var interval = new AdaptivePollInterval(TimeSpan.FromMilliseconds(1));
        var tooShortCalls = new List<TimeSpan>();
        var persistedCalls = new List<TimeSpan>();

        //act
        LocalStorageWatchPolling.Run(
            DummyAdapter, DummyAccount,
            new List<IHomeScreenSyncModule> { moduleA, moduleB },
            (clip, name) => { },
            (ex, name) => { },
            (total, slowest, newInterval) => tooShortCalls.Add(newInterval),
            newInterval => persistedCalls.Add(newInterval),
            interval,
            CancellationToken.None,
            maxIterations: 3);

        //assert
        tooShortCalls.Should().HaveCount(1);
        persistedCalls.Should().HaveCount(1);
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
