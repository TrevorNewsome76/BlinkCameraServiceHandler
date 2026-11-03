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
}
