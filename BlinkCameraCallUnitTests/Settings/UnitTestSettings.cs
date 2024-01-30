using BlinkCameraCall;
using BlinkCommon.Interfaces;
using FluentAssertions;
using Shadow.Quack;

using System.IO;
using BlinkCommon.Extensions;
using Xunit.Abstractions;
using System.Runtime;
using System;

namespace BlinkCameraCallUnitTests.Settings;

[Collection("Sequential")]
public class UnitTestSettings(ITestOutputHelper output)
{
    [Fact]
    public void Test_SuccessfulCreateSettingsFile()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var expectedResult = MockSettings.CreateSettings();

        // If File already exists then clean down
        if (File.Exists($"{path}settings.json")) File.Delete($"{path}settings.json");

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
        if (File.Exists($"{path}settings.json")) File.Delete($"{path}settings.json");
        File.WriteAllText($"{path}settings.json", MockSettings.CreateSettings().Serialize());

        // act
        actualResult.Load(AppContext.BaseDirectory);
        
        // assert
        Assert.True(File.Exists($"{path}settings.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);

        // Clear up
        if (File.Exists($"{path}settings.json")) File.Delete($"{path}settings.json");
        Assert.True(!File.Exists($"{path}settings.json"));
    }
}

