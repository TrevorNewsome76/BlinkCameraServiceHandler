using System.Net;
using System.Text;
using BlinkCameraCall.Driver;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Driver;

public class UnitTest_HttpClientApiHandler
{
    private class FakeHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body),
            });
    }

    [Fact]
    public void Test_GetBytes_NonSuccessStatus_ReturnsNull()
    {
        using var handler = new HttpClientApiHandler(new FakeHttpMessageHandler(HttpStatusCode.NotFound, "not found"));

        var result = handler.GetBytes("https://example.invalid/missing.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public void Test_GetBytes_SuccessStatus_ReturnsBytes()
    {
        using var handler = new HttpClientApiHandler(new FakeHttpMessageHandler(HttpStatusCode.OK, "hello"));

        var result = handler.GetBytes("https://example.invalid/ok.jpg");

        result.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("hello"));
    }
}
