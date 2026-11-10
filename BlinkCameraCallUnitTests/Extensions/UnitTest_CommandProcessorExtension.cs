using BlinkCommon.Interfaces;

using FluentAssertions;

using Shadow.Quack;

using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests.Extensions;

[Collection("Sequential")]
public class UnitTest_CommandProcessorExtension(ITestOutputHelper output)
{
    [Fact]
    public void Test_CommandSplitOnNullStringPass()
    {
        //assign
        var expectedResult = Duck.Implement<ICommandAndArguments>(
            new
            {
                Message = "Null or empty command. Cannot process nothing.",
            });
        string consoleCommand = null!;

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitOnEmptyStringPass()
    {
        //assign
        var expectedResult = Duck.Implement<ICommandAndArguments>(
            new
            {
                Message = "Null or empty command. Cannot process nothing.",
            });
        var consoleCommand = string.Empty;

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitSingleCommand()
    {
        //assign
        string consoleCommand = "FooBar";
        var expectedResult = Duck.Implement<ICommandAndArguments>(
            new
            {
                Command = "FooBar",
                Message = "Unknown command."
            });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitMultipleArgumentsInvalidCommand()
    {
        //assign
        string consoleCommand = "FOOBAR -eemailAddress -pPAssword";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "FOOBAR",
            Arguments = new string[]
            {
                "eemailAddress",
                "pPAssword",
            },
            Message = "Unknown command."
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitMultipleArgumentsValidCommand()
    {
        //assign
        string consoleCommand = "LOGIN -eemailAddress -pPAssword";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "LOGIN",
            Arguments = new string[]
            {
                "eemailAddress",
                "pPAssword",
            },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitBarePositional_Cd_WithMultiWordTarget()
    {
        //assign
        string consoleCommand = "cd Front Door";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "cd",
            Arguments = new string[] { "Front Door" },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitBarePositional_Cd_NoTarget()
    {
        //assign
        string consoleCommand = "cd";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "cd",
            Arguments = new string[] { "" },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitBarePositional_Ls_IgnoresTrailingText()
    {
        //assign
        string consoleCommand = "ls extra";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "ls",
            Arguments = new string[] { "extra" },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitBarePositional_Trigger_NoTarget()
    {
        //assign
        string consoleCommand = "trigger";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "trigger",
            Arguments = new string[] { "" },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitDownload_FlagStyle_StillUsesLegacyParsing()
    {
        //assign
        string consoleCommand = "download -i123";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "download",
            Arguments = new string[] { "i123" },
            BareDownload = false,
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitDownload_BareName_SetsBareDownloadFlag()
    {
        //assign
        string consoleCommand = "download Front Door Clip";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "download",
            Arguments = new string[] { "Front Door Clip" },
            BareDownload = true,
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitWatch_WithIntervalFlag_IsValid()
    {
        //assign
        string consoleCommand = "watch -s30";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "watch",
            Arguments = new string[] { "s30" },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitUnwatch_NoArguments_IsValid()
    {
        //assign
        string consoleCommand = "unwatch";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "unwatch",
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitArm_NoArguments_IsValid()
    {
        //assign
        string consoleCommand = "arm";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "arm",
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Test_CommandSplitDisarm_NoArguments_IsValid()
    {
        //assign
        string consoleCommand = "disarm";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "disarm",
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}