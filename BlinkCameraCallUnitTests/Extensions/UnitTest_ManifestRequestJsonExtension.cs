using BlinkCommon.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_ManifestRequestJsonExtension
{
    // Trimmed from a real logged manifest-request response that exposed this exact bug
    // class before (see CommandStatusJsonExtension): a real id large enough to exceed
    // Int32.MaxValue crashes Shadow.Quack's JsonProxy, which the old Deserialize<T>()
    // call silently swallowed into an all-default (Id = 0) object, misreporting a
    // genuine success as "Blink rejected the request".
    private const string RealShapedPayload = "{\"id\":3800203652,\"network_id\":856598}";

    [Fact]
    public void Test_ParseManifestRequest_RealShapedPayload_ReadsIdCorrectly()
    {
        var result = RealShapedPayload.ParseManifestRequest();

        result.Id.Should().Be(3800203652L);
    }

    [Fact]
    public void Test_ParseManifestRequest_SimpleExistingShape_StillParsesIdentically()
    {
        var result = "{\"id\":600}".ParseManifestRequest();

        result.Id.Should().Be(600L);
    }

    [Fact]
    public void Test_ParseManifestRequest_MalformedJson_ReturnsAllDefaultWithoutThrowing()
    {
        var result = "{not json".ParseManifestRequest();

        result.Id.Should().Be(0L);
    }

    [Fact]
    public void Test_ParseManifestRequest_NullOrEmpty_ReturnsAllDefault()
    {
        ((string?)null).ParseManifestRequest().Id.Should().Be(0L);
        "".ParseManifestRequest().Id.Should().Be(0L);
    }
}
