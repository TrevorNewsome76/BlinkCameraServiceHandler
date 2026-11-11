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
public class UnitTest_EventWatchPolling
{
    private class FakeApiTransactions(params Func<IMediaChangedResponse?>[] responses) : IApiTransactions
    {
        private int _callCount;

        public int CallCount => _callCount;

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
        public IManifestRequestResponse? SystemRequestLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId) => null;
        public ILocalStorageManifestResult? SystemGetLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId, long manifestRequestId, TimeSpan? pollInterval = null, int maxRetries = 120, CancellationToken cancellationToken = default) => null;
        public string BuildLocalStorageClipUrl(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId) => string.Empty;
        public ICommandStatusResponse? SystemPrepareLocalStorageClip(IAuthAccount account, long networkId, long syncModuleId, long manifestId, long clipId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;

        public IMediaChangedResponse? SystemGetRecentMedia(IAuthAccount account, DateTime sinceUtc)
        {
            var index = Math.Min(_callCount, responses.Length - 1);
            _callCount++;
            return responses[index]();
        }
    }

    private static readonly IAdapter DummyAdapter = Duck.Implement<IAdapter>();
    private static readonly IAuthAccount DummyAccount = Duck.Implement<IAuthAccount>(new());

    private static IMediaChangedResponse TwoItems() => Duck.Implement<IMediaChangedResponse>(new
    {
        Media = Duck.Implement<List<ICloudMediaItem>>(new[]
        {
            new { Id = 3001L, Network_Id = 263178L, Device_Id = 321343L, Device_Name = "Pergoda", Created_At = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), Media = "/media/clip1.mp4", Deleted = false },
            new { Id = 3002L, Network_Id = 263178L, Device_Id = 321380L, Device_Name = "Garage", Created_At = new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc), Media = "/media/clip2.mp4", Deleted = false },
        }),
    });

    private static IMediaChangedResponse NoItems() => Duck.Implement<IMediaChangedResponse>(new
    {
        Media = Duck.Implement<List<ICloudMediaItem>>(Array.Empty<object>()),
    });

    private static IMediaChangedResponse OneDeletedItem() => Duck.Implement<IMediaChangedResponse>(new
    {
        Media = Duck.Implement<List<ICloudMediaItem>>(new[]
        {
            new { Id = 4001L, Network_Id = 263178L, Device_Id = 321343L, Device_Name = "Pergoda", Created_At = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), Media = "/media/clip9.mp4", Deleted = true },
        }),
    });

    [Fact]
    public void Test_Run_FirstPoll_ReportsAllItemsAsNew()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(TwoItems));
        var reported = new List<long>();

        //act
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, new AdaptivePollInterval(TimeSpan.Zero), maxIterations: 1);

        //assert
        reported.Should().Equal(3001L, 3002L);
    }

    [Fact]
    public void Test_Run_RepeatedPoll_DoesNotReportSameItemTwice()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(TwoItems));
        var reported = new List<long>();

        //act
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, new AdaptivePollInterval(TimeSpan.Zero), maxIterations: 2);

        //assert
        reported.Should().Equal(3001L, 3002L);
    }

    [Fact]
    public void Test_Run_NoEvents_ReportsNothing()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(NoItems));
        var reported = new List<long>();

        //act
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, new AdaptivePollInterval(TimeSpan.Zero), maxIterations: 1);

        //assert
        reported.Should().BeEmpty();
    }

    [Fact]
    public void Test_Run_DeletedItem_IsIgnored()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(OneDeletedItem));
        var reported = new List<long>();

        //act
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, new AdaptivePollInterval(TimeSpan.Zero), maxIterations: 1);

        //assert
        reported.Should().BeEmpty();
    }

    [Fact]
    public void Test_Run_AlreadyCancelled_DoesNotPoll()
    {
        //assign
        Shelf.Clear();
        var transactions = new FakeApiTransactions(TwoItems);
        Shelf.ShelveInstance<IApiTransactions>(transactions);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        //act
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => { }, ex => { }, cts.Token, new AdaptivePollInterval(TimeSpan.Zero), maxIterations: 5);

        //assert
        transactions.CallCount.Should().Be(0);
    }

    [Fact]
    public void Test_Run_PollThrows_CallsOnErrorAndKeepsGoing()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(
            () => throw new InvalidOperationException("boom"),
            TwoItems));
        var reported = new List<long>();
        var errors = new List<string>();

        //act
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => errors.Add(ex.Message), CancellationToken.None, new AdaptivePollInterval(TimeSpan.Zero), maxIterations: 2);

        //assert
        errors.Should().Equal("boom");
        reported.Should().Equal(3001L, 3002L);
    }

    [Fact]
    public void Test_Run_RoundSlowerThanInterval_CallsOnIntervalTooShortAndPersisted()
    {
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(() =>
        {
            Thread.Sleep(50);
            return NoItems();
        }));
        var interval = new AdaptivePollInterval(TimeSpan.FromMilliseconds(10));
        var tooShortCalls = new List<(TimeSpan Total, TimeSpan Slowest, TimeSpan NewInterval)>();
        var persistedCalls = new List<TimeSpan>();

        //act
        EventWatchPolling.Run(
            DummyAdapter, DummyAccount, item => { }, ex => { }, CancellationToken.None,
            interval,
            (total, slowest, newInterval) => tooShortCalls.Add((total, slowest, newInterval)),
            newInterval => persistedCalls.Add(newInterval),
            maxIterations: 1);

        //assert
        tooShortCalls.Should().HaveCount(1);
        persistedCalls.Should().HaveCount(1);
        tooShortCalls[0].Total.Should().Be(tooShortCalls[0].Slowest, "WATCH's round is always a single item");
        tooShortCalls[0].NewInterval.Should().Be(interval.Current, "the callback reports the interval AdaptivePollInterval just retuned to");
        persistedCalls[0].Should().Be(interval.Current);
    }

    [Fact]
    public void Test_Run_RoundFasterThanInterval_NeitherCallbackInvoked()
    {
        //assign
        Shelf.Clear();
        var cts = new CancellationTokenSource();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(() =>
        {
            // Cancel from inside the fast round itself so the test doesn't have to sit
            // through the 30s post-round wait to reach its assertions — this test is about
            // whether the retune callbacks fire, not about wait interruption (that's covered
            // by Test_Run_CancelledDuringPostRoundWait_StopsPromptlyRatherThanWaitingFullInterval).
            cts.Cancel();
            return NoItems();
        }));
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(30));
        var tooShortCalled = false;
        var persistedCalled = false;

        //act
        EventWatchPolling.Run(
            DummyAdapter, DummyAccount, item => { }, ex => { }, cts.Token,
            interval,
            (_, _, _) => tooShortCalled = true,
            _ => persistedCalled = true,
            maxIterations: 5);

        //assert
        tooShortCalled.Should().BeFalse();
        persistedCalled.Should().BeFalse();
    }

    [Fact]
    public void Test_Run_CancelledDuringRound_DoesNotRetuneOrPersistAfterCancellation()
    {
        // Proves a round that finishes AFTER cancellation was already requested mid-round does
        // NOT fire either retune callback, even though the round itself was genuinely slower than
        // the current interval (so it WOULD have retuned had cancellation not already landed).
        // Mirrors LocalStorageWatchPolling.Run's existing
        // `if (cancelledMidRound || token.IsCancellationRequested) break;` guard before its own
        // retune call — EventWatchPolling.Run lacked the equivalent guard before this fix, so a
        // just-cancelled round's trailing retune could race and overwrite an explicit `-s<secs>`
        // the user set a moment later via a fresh WATCH.
        //assign
        Shelf.Clear();
        var cts = new CancellationTokenSource();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(() =>
        {
            cts.Cancel();
            Thread.Sleep(20);
            return NoItems();
        }));
        var interval = new AdaptivePollInterval(TimeSpan.FromMilliseconds(1));
        var tooShortCalled = false;
        var persistedCalled = false;

        //act
        EventWatchPolling.Run(
            DummyAdapter, DummyAccount, item => { }, ex => { }, cts.Token,
            interval,
            (_, _, _) => tooShortCalled = true,
            _ => persistedCalled = true,
            maxIterations: 5);

        //assert
        tooShortCalled.Should().BeFalse();
        persistedCalled.Should().BeFalse();
    }

    [Fact]
    public void Test_Run_CancelledDuringPostRoundWait_StopsPromptlyRatherThanWaitingFullInterval()
    {
        // Proves cancellation requested *during* the post-round wait is honored immediately,
        // not only once the wait fully elapses. Uses CancelAfter (an independent timer) rather
        // than cancelling synchronously inside the fake's response, so the signal genuinely
        // arrives mid-wait regardless of whether the loop's own thread is currently blocked in
        // a non-interruptible Thread.Sleep (the old, buggy behavior) or waiting on the token's
        // WaitHandle (the fix). A real, large interval (30s) combined with asserting elapsed
        // time is well under that makes the old uninterruptible behavior obviously fail this test.
        //assign
        Shelf.Clear();
        Shelf.ShelveInstance<IApiTransactions>(new FakeApiTransactions(NoItems));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(30));

        //act
        var stopwatch = Stopwatch.StartNew();
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => { }, ex => { }, cts.Token, interval, maxIterations: 1000);
        stopwatch.Stop();

        //assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "cancellation during the post-round wait should interrupt it immediately, not block for the full 30s interval");
    }
}
