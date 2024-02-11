using BlinkCommon.Interfaces;
using FluentAssertions;
using Shadow.Quack;
using BlinkCommon.Extensions;
using Xunit.Abstractions;
using BlinkCameraCall.Extensions;

namespace BlinkCameraCallUnitTests.Settings;

[Collection("Sequential")]
public class UnitTestSettings(ITestOutputHelper output)
{

    private void HelperMethod_RemoveSettingsFile(string path)
    {
        if (File.Exists($"{path}settings.json")) File.Delete($"{path}settings.json");
        Assert.True(!File.Exists($"{path}settings.json"));
    }

    private void HelperMethod_CreateDefaultSettingsFile(string path)
    {
        File.WriteAllText($"{path}settings.json", MockSettings.CreateSettings().Serialize());
        Assert.True(File.Exists($"{path}settings.json"));
    }


    [Fact]
    public void Test_SuccessfulCreateSettingsFile()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var expectedResult = MockSettings.CreateSettings();

        HelperMethod_RemoveSettingsFile(path);

        // act
        actualResult.Load(AppContext.BaseDirectory);

        // assert
        Assert.True(!File.Exists($"{path}settings.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_SuccessfulLoadSettingsFile()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var expectedResult = MockSettings.CreateSettings();

        // If File already exists then clean down and create new one
        HelperMethod_RemoveSettingsFile(path);
        HelperMethod_CreateDefaultSettingsFile(path);

        // act
        actualResult.Load(AppContext.BaseDirectory);
        
        // assert
        Assert.True(File.Exists($"{path}settings.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);

        // output
        output.WriteLine($"url: {path}settings.json");

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }

    [Fact]
    public void Test_SuccessfulSaveNewSettingsFile()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var expectedResult = MockSettings.CreateSettings("Foo", "Bar");

        // If File already exists then clean down
        HelperMethod_RemoveSettingsFile(path);

        // act
        MockSettings.CreateSettings("Foo", "Bar").Save(AppContext.BaseDirectory);
        actualResult.Load(AppContext.BaseDirectory);

        // assert
        Assert.True(File.Exists($"{path}settings.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }

    [Fact]
    public void Test_SuccessfulSaveOverExistingSettingsFile()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var expectedResult = MockSettings.CreateSettings("Foo", "Bar");

        // If File already exists then clean down
        HelperMethod_RemoveSettingsFile(path);
        HelperMethod_CreateDefaultSettingsFile(path);

        // act
        MockSettings.CreateSettings("Foo", "Bar").Save(AppContext.BaseDirectory);
        actualResult.Load(AppContext.BaseDirectory);

        // assert
        Assert.True(File.Exists($"{path}settings.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }
}

