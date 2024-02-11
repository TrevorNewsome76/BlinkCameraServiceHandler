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
}