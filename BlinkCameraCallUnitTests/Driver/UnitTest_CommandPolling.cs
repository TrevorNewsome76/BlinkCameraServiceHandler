using BlinkCameraCall.Driver;
using BlinkCommon.Interfaces;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Driver;

public class UnitTest_CommandPolling
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

    private const string Incomplete = "{\"id\":1,\"network_id\":2,\"complete\":false,\"status_code\":908}";
    private const string Complete = "{\"id\":1,\"network_id\":2,\"complete\":true,\"status_code\":908}";
    private const string Failed = "{\"id\":1,\"network_id\":2,\"complete\":false,\"status_code\":200}";

    [Fact]
    public void Test_WaitForCommandComplete_CompletesOnFirstPoll()
    {
        var api = new FakeApiMethods(Complete);

        var result = CommandPolling.WaitForCommandComplete(api, "http://example/command/1", TimeSpan.Zero, 3);

        result!.Complete.Should().BeTrue();
    }

    [Fact]
    public void Test_WaitForCommandComplete_CompletesAfterSeveralPolls()
    {
        var api = new FakeApiMethods(Incomplete, Incomplete, Complete);

        var result = CommandPolling.WaitForCommandComplete(api, "http://example/command/1", TimeSpan.Zero, 5);

        result!.Complete.Should().BeTrue();
    }

    [Fact]
    public void Test_WaitForCommandComplete_TimesOutAfterMaxRetries_ReturnsNull()
    {
        var api = new FakeApiMethods(Incomplete);

        var result = CommandPolling.WaitForCommandComplete(api, "http://example/command/1", TimeSpan.Zero, 3);

        result.Should().BeNull();
    }

    [Fact]
    public void Test_WaitForCommandComplete_NonSuccessStatusCode_ReturnsNullImmediately()
    {
        var api = new FakeApiMethods(Failed);

        var result = CommandPolling.WaitForCommandComplete(api, "http://example/command/1", TimeSpan.Zero, 120);

        result.Should().BeNull();
        api.CallCount.Should().Be(1);
    }
}
