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
        if (File.Exists($"{path}config.json")) File.Delete($"{path}config.json");
        Assert.True(!File.Exists($"{path}config.json"));
    }

    private void HelperMethod_CreateDefaultSettingsFile(string path)
    {
        File.WriteAllText($"{path}config.json", MockSettings.CreateSettings().Serialize());
        Assert.True(File.Exists($"{path}config.json"));
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
        Assert.True(!File.Exists($"{path}config.json"));
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
        Assert.True(File.Exists($"{path}config.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);

        // output
        output.WriteLine($"url: {path}config.json");

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
        Assert.True(File.Exists($"{path}config.json"));
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
        Assert.True(File.Exists($"{path}config.json"));
        actualResult.Should().BeEquivalentTo(expectedResult);

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }

    [Fact]
    public void Test_Load_FileMissingNewFields_DefaultsBothIntervals()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());

        HelperMethod_RemoveSettingsFile(path);

        // Hand-write a config.json missing the interval fields entirely.
        File.WriteAllText($"{path}config.json", """
        {
            "Email": "test@email.com",
            "Password": "PAssword",
            "BaseUrl": "https://rest-prod.immedia-semi.com",
            "HardwareId": ""
        }
        """);

        // act
        actualResult.Load(path);

        // assert
        Assert.Equal(20, actualResult.WatchIntervalSeconds);
        Assert.Equal(5, actualResult.LocalWatchIntervalSeconds);

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }

    [Fact]
    public void Test_Load_FileHasStoredIntervals_UsesStoredValues()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var storedSettings = Duck.Implement<IBlinkSettings>(new
        {
            BaseUrl = "https://rest-prod.immedia-semi.com",
            Email = "test@email.com",
            Password = "PAssword",
            HardwareId = string.Empty,
            WatchIntervalSeconds = 45,
            LocalWatchIntervalSeconds = 8,
        });

        HelperMethod_RemoveSettingsFile(path);
        storedSettings.Save(path);

        // act
        actualResult.Load(path);

        // assert
        Assert.Equal(45, actualResult.WatchIntervalSeconds);
        Assert.Equal(8, actualResult.LocalWatchIntervalSeconds);

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }

    [Fact]
    public void Test_SaveThenLoad_RoundTripsBothIntervals()
    {
        // assign
        var path = $"{AppContext.BaseDirectory}";
        var actualResult = Duck.Implement<IBlinkSettings>(new());
        var settingsToSave = Duck.Implement<IBlinkSettings>(new
        {
            BaseUrl = "https://rest-prod.immedia-semi.com",
            Email = "test@email.com",
            Password = "PAssword",
            HardwareId = string.Empty,
            WatchIntervalSeconds = 30,
            LocalWatchIntervalSeconds = 10,
        });

        HelperMethod_RemoveSettingsFile(path);

        // act
        settingsToSave.Save(path);
        actualResult.Load(path);

        // assert
        Assert.Equal(30, actualResult.WatchIntervalSeconds);
        Assert.Equal(10, actualResult.LocalWatchIntervalSeconds);

        // Clear up
        HelperMethod_RemoveSettingsFile(path);
    }
}

