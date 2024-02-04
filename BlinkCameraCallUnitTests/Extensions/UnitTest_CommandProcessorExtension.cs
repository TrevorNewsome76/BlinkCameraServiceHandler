using BlinkCommon.Interfaces;

using FluentAssertions;

using Shadow.Quack;

using System.Net.Mail;

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
                ErrorMessage = "Null or empty command. Cannot process nothing.",
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
                ErrorMessage = "Null or empty command. Cannot process nothing.",
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
                Command = "foobar",
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
        string consoleCommand = "FOOBAR -eemailAddress -ppassword";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "foobar",
            Arguments = new string[]
            {
                "eemailaddress",
                "ppassword",
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
        string consoleCommand = "LOGIN -eemailAddress -ppassword";
        var expectedResult = Duck.Implement<ICommandAndArguments>(new
        {
            Command = "login",
            Arguments = new string[]
            {
                "eemailaddress",
                "ppassword",
            },
        });

        //act
        var actualResult = consoleCommand.ProcessCommandString();

        //assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}