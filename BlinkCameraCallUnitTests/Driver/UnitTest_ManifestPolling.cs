using System.Diagnostics;
using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Driver;

public class UnitTest_ManifestPolling
{
    private class FakeApiMethods(params string[] responses) : IApiMethods
    {
        private int _callCount;

        public bool SetAccessToken(string accessToken) => true;
        public string? Post(string url, List<KeyValuePair<string, string>> parameters) => null;
        public string? Post(string url, string body) => null;
        public string? Post(string url) => null;
        public string? Put(string url, string serializedJsonString) => null;
        public byte[]? GetBytes(string url) => null;

        public string? Get(string url)
        {
            var index = Math.Min(_callCount, responses.Length - 1);
            _callCount++;
            return responses[index];
        }

        public int CallCount => _callCount;
    }

    private const string NotReady = "{\"id\":7}";
    private const string Ready = "{\"manifest_id\":99,\"clips\":[{\"id\":1,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-01T12:00:00+00:00\",\"size\":12345}]}";

    [Fact]
    public void Test_WaitForManifestReady_ReadyOnFirstPoll()
    {
        var api = new FakeApiMethods(Ready);

        var result = ManifestPolling.WaitForManifestReady(api, "http://example/manifest/request/7", TimeSpan.Zero, 3);

        result!.Manifest_Id.Should().Be(99);
        result.Clips.Should().ContainSingle();
    }

    [Fact]
    public void Test_WaitForManifestReady_ReadyAfterSeveralPolls()
    {
        var api = new FakeApiMethods(NotReady, NotReady, Ready);

        var result = ManifestPolling.WaitForManifestReady(api, "http://example/manifest/request/7", TimeSpan.Zero, 5);

        result!.Clips.Should().ContainSingle();
    }

    [Fact]
    public void Test_WaitForManifestReady_TimesOutAfterMaxRetries_ReturnsNull()
    {
        var api = new FakeApiMethods(NotReady);

        var result = ManifestPolling.WaitForManifestReady(api, "http://example/manifest/request/7", TimeSpan.Zero, 3);

        result.Should().BeNull();
    }

    [Fact]
    public void Test_WaitForManifestReady_AlreadyCancelled_ThrowsBeforeFirstGet()
    {
        var api = new FakeApiMethods(NotReady);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Action act = () => ManifestPolling.WaitForManifestReady(api, "http://example/manifest/request/7", TimeSpan.Zero, 3, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        api.CallCount.Should().Be(0);
    }

    [Fact]
    public void Test_WaitForManifestReady_CancelledDuringWait_StopsBeforeNextAttempt()
    {
        // Proves cancellation requested *during* the poll wait is honored immediately, not
        // only once the wait fully elapses. Uses CancelAfter (an independent timer) rather than
        // cancelling synchronously from inside a Get call, so the signal genuinely arrives
        // mid-wait regardless of whether the loop's own thread is currently blocked in a
        // non-interruptible Thread.Sleep (the old, buggy behavior) or waiting on the token's
        // WaitHandle (the fix). A real, long pollInterval (5s) combined with asserting elapsed
        // time is well under that makes the old uninterruptible behavior obviously fail this test.
        var api = new FakeApiMethods(NotReady);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var stopwatch = Stopwatch.StartNew();
        Action act = () => ManifestPolling.WaitForManifestReady(api, "http://example/manifest/request/7", TimeSpan.FromSeconds(5), 120, cts.Token);
        act.Should().Throw<OperationCanceledException>();
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "cancellation during the wait should interrupt it immediately, not block for the full 5s interval");
        api.CallCount.Should().BeLessThan(120);
    }

    [Fact]
    public void Test_WaitForManifestReady_NotCancelled_StillReturnsResultOnceCompleteFlagAppears()
    {
        var api = new FakeApiMethods(NotReady, Ready);

        var result = ManifestPolling.WaitForManifestReady(api, "http://example/manifest/request/7", TimeSpan.Zero, 5);

        result!.Clips.Should().ContainSingle();
    }
}
