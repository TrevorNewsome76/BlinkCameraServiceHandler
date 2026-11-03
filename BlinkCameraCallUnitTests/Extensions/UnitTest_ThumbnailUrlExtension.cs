using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_ThumbnailUrlExtension
{
    [Fact]
    public void Test_BuildThumbnailUrl_NumericTimestamp_BuildsFixedShapeUrl()
    {
        var url = ThumbnailUrlExtension.BuildThumbnailUrl("1705051738", "007", 239361, 263178, 321343, isDoorbell: false);

        url.Should().Be("https://rest-007.immedia-semi.com/api/v3/media/accounts/239361/networks/263178/camera/321343/thumbnail/thumbnail.jpg?ts=1705051738&ext=");
    }

    [Fact]
    public void Test_BuildThumbnailUrl_OldApiPath_AppendsJpg()
    {
        var url = ThumbnailUrlExtension.BuildThumbnailUrl(
            "/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail",
            "007", 239361, 263178, 321343, isDoorbell: false);

        url.Should().Be("https://rest-007.immedia-semi.com/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg");
    }

    [Fact]
    public void Test_BuildThumbnailUrl_AlreadyEndsWithExt_UsedAsIs()
    {
        var url = ThumbnailUrlExtension.BuildThumbnailUrl(
            "/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg?ts=1705051738&ext=",
            "007", 239361, 263178, 321343, isDoorbell: false);

        url.Should().Be("https://rest-007.immedia-semi.com/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg?ts=1705051738&ext=");
    }

    [Fact]
    public void Test_BuildThumbnailUrl_Doorbell_UsesDoorbellSegment()
    {
        var url = ThumbnailUrlExtension.BuildThumbnailUrl("1705051747", "007", 239361, 263178, 5009, isDoorbell: true);

        url.Should().Be("https://rest-007.immedia-semi.com/api/v3/media/accounts/239361/networks/263178/doorbell/5009/thumbnail/thumbnail.jpg?ts=1705051747&ext=");
    }
}
