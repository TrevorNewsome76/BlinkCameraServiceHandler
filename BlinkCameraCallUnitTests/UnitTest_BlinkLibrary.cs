﻿using BlinkCameraCall;
using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests;

[Collection("Sequential")]
public class UnitTest_BlinkLibrary(ITestOutputHelper output)
{

    [Fact]
    public void Test_LoginFailedInvalidCredentials()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings("-ufoo", "-pBar"));
        var arguments = new string[]
        {
            new string("ufoo"),
            new string("pBar"),
        };
        var expected = "Login to the Blink Service failed: Invalid credentials";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Login(arguments);

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_LoginPassedValidCredentials()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var arguments = new string[]
        {
            new string("utest@email.com"),
            new string("pPAssword"),
        };
        var expected = "Login to the Blink Service successful.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Login(arguments);

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_LoginNullArgumentsFailed()
    {
        //assign
        var expectedResult = "Username and/or password not supplied. Use /u<username> and /p<password> after the login command.";
        var arguments = new string[3];
        arguments = null;

        //act
        var exception = Assert.Throws<ArgumentException>(() =>
            arguments.ExtractUsernameAndPassword());


        exception.Message.Should().BeEquivalentTo(expectedResult);
        if (exception.InnerException != null) Assert.Null(exception.InnerException);


        //Results
        output.WriteLine(exception.Message);
    }

    [Fact] public void Test_LogoutFailedNotCurrentlyLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Logout();

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_LogoutSuccessful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Successfully logged out.";
        var arguments = new string[]
        {
            new string("utest@email.com"),
            new string("pPAssword"),
        };

        //act
        var blinkLib = new BlinkLibrary(mockAdapter);
        
        var loginResult = blinkLib.Login(arguments);
        
        Assert.NotEmpty(loginResult);

        var actualResult = blinkLib .Logout();

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    //[Fact]
    //public void Test_HelpSuccessful()
    //{
    //    //assign
    //    var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
    //    var expected = new string("LOGIN            Logs into Blink account (using settings file." + System .Environment.NewLine +
    //                              "PIN <code>       Verifies sms pin number sent after new login." + System.Environment.NewLine +
    //                              "QUIT, EXIT       Exits program." + System.Environment.NewLine +
    //                              "HELP             Provides Help information for Windows commands." + System.Environment.NewLine);

    //    //act
    //    var blinkLib = new BlinkLibrary(mockAdapter);

    //    var actualResult = blinkLib.Help(Array.Empty<string>());

    //    //assert
    //    actualResult.Should().BeEquivalentTo(expected);

    //    //Results
    //    output.WriteLine("Message: {0}", expected);
    //}

    [Fact]
    public void Test_VerifyPinSuccessful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "(1626) Client has been successfully verified";
        var arguments = new string[]
        {
            new string("utest@email.com"),
            new string("pPAssword"),
            new string("v987654"),
        };

        //act
        var blinkLib = new BlinkLibrary(mockAdapter);

        var loginResult = blinkLib.Login(arguments);

        Assert.NotEmpty(loginResult);

        var actualResult = blinkLib.VerifyPin(arguments);

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_VerifyPinFailedIncorrectPin()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "(1621) Invalid PIN";
        var arguments = new string[]
        {
            new string("utest@email.com"),
            new string("pPAssword"),
            new string("v123456"),
        };

        //act
        var blinkLib = new BlinkLibrary(mockAdapter);

        var loginResult = blinkLib.Login(arguments);
        
        Assert.NotEmpty(loginResult);

        var actualResult = blinkLib.VerifyPin(arguments);

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_VerifyPinFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";
        var arguments = new string[]
        {
            new string("utest@email.com"),
            new string("pPAssword"),
            new string("v987654"),
        };

        //act
        var actualResult = new BlinkLibrary(mockAdapter).VerifyPin(arguments);

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_ExtractPinCodeSuccessful()
    {
        //assign
        var expected = "987654";
        var arguments = new[]
        {
            new string("v987654"),
        };

        //act
        var actualResult = arguments.ExtractPinCode();

        //assert
        actualResult.Should().BeEquivalentTo(expected);

        //Results
        output.WriteLine("Message: {0}", expected);
    }

    [Fact]
    public void Test_ExtractPinCodePinCodeParameterFailed()
    {
        //assign
        var expectedResult = "Pin code not supplied. Use /v<pincode> Verify command.";
        var arguments = new[]
        {
            new string("utest@email.com"),
            new string("pPAssword"),
        };

        //act
        var exception = Assert.Throws<ArgumentException>(() =>
            arguments.ExtractPinCode());

        //assert
        exception.Message.Should().BeEquivalentTo(expectedResult);
        if (exception.InnerException != null) Assert.Null(exception.InnerException);

        //Results
        output.WriteLine(exception.Message);
    }

    [Fact]
    public void Test_ExtractPinCodeNullArgumentsFailed()
    {
        //assign
        var expectedResult = "Pin code not supplied. Use /v<pincode> Verify command.";
        var arguments = new string[3];
        arguments = null;

        //act
        var exception = Assert.Throws<ArgumentException>(() =>
            arguments.ExtractPinCode());


        exception.Message.Should().BeEquivalentTo(expectedResult);
        if (exception.InnerException != null) Assert.Null(exception.InnerException);


        //Results
        output.WriteLine(exception.Message);
    }
}