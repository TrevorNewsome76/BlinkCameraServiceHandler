using System.Text;
using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_MediaFileExtension : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void Test_SaveMediaFile_SanitizesDisallowedCharacters()
    {
        var path = MediaFileExtension.SaveMediaFile("Front/Door:Cam*", Encoding.UTF8.GetBytes("x"), "jpg", _tempDirectory);

        Path.GetFileName(path).Should().NotContain("/").And.NotContain(":").And.NotContain("*");
    }

    [Fact]
    public void Test_SaveMediaFile_CreatesDirectoryIfMissing()
    {
        Directory.Exists(_tempDirectory).Should().BeFalse();

        MediaFileExtension.SaveMediaFile("Front Door", Encoding.UTF8.GetBytes("x"), "jpg", _tempDirectory);

        Directory.Exists(_tempDirectory).Should().BeTrue();
    }

    [Fact]
    public void Test_SaveMediaFile_WritesExactBytes()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");

        var path = MediaFileExtension.SaveMediaFile("Front Door", bytes, "jpg", _tempDirectory);

        File.ReadAllBytes(path).Should().BeEquivalentTo(bytes);
    }
}
