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
        public ICommandStatusResponse? SystemEnableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemDisableCamera(IAuthAccount account, long networkId, long cameraId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemEnableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemDisableDoorbell(IAuthAccount account, long networkId, long doorbellId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemArmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public ICommandStatusResponse? SystemDisarmNetwork(IAuthAccount account, long networkId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
        public IManifestRequestResponse? SystemRequestLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId) => null;
        public ILocalStorageManifestResult? SystemGetLocalStorageManifest(IAuthAccount account, long networkId, long syncModuleId, long manifestRequestId, TimeSpan? pollInterval = null, int maxRetries = 120) => null;
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
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, TimeSpan.Zero, maxIterations: 1);

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
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, TimeSpan.Zero, maxIterations: 2);

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
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, TimeSpan.Zero, maxIterations: 1);

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
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => { }, CancellationToken.None, TimeSpan.Zero, maxIterations: 1);

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
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => { }, ex => { }, cts.Token, TimeSpan.Zero, maxIterations: 5);

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
        EventWatchPolling.Run(DummyAdapter, DummyAccount, item => reported.Add(item.Id), ex => errors.Add(ex.Message), CancellationToken.None, TimeSpan.Zero, maxIterations: 2);

        //assert
        errors.Should().Equal("boom");
        reported.Should().Equal(3001L, 3002L);
    }
}
