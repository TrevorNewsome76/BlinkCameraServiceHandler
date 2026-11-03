using BlinkCameraCall;
using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using Dependency;
using FluentAssertions;
using Xunit.Abstractions;

namespace BlinkCameraCallUnitTests;

[Collection("Sequential")]
public class UnitTest_BlinkLibrary(ITestOutputHelper output)
{

    [Fact]
    public void Test_MapFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Map();

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_MapSuccessful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected =
            "Network: Home Front (disarmed)" + Environment.NewLine +
            "  Sync Module: My Blink Sync Module  [online, fw 16.0.8, wifi 5]" + Environment.NewLine +
            "  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "  Camera: Garage  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            Environment.NewLine +
            "Network: Home Back (disarmed)" + Environment.NewLine +
            "  Sync Module: My Blink Sync Module  [offline, fw 4.4.8, wifi 5]" + Environment.NewLine +
            "  (no cameras or doorbells)";

        //act
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        var actualResult = blinkLib.Map();

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_SnapFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Snap(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_SnapFailedNoDevicesLoaded()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "Run MAP or GET first to load your devices.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);

        //act
        var actualResult = blinkLib.Snap(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_SnapFailedDeviceNotFound()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "No single device named 'Unknown Camera' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Snap(new[] { "nUnknown Camera" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_SnapSuccessful_Camera()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "New image captured for 'Pergoda'. Run IMAGE -nPergoda to download it.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();
        var callsBeforeSnap = apiMethods.HomeScreenCallCount;

        //act
        var actualResult = blinkLib.Snap(new[] { "nPergoda" }, TimeSpan.Zero, 1);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
        apiMethods.HomeScreenCallCount.Should().Be(callsBeforeSnap + 1);
    }

    [Fact]
    public void Test_SnapTimedOut()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "Blink did not finish capturing a new image for 'Garage' in time.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Snap(new[] { "nGarage" }, TimeSpan.Zero, 2);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DeviceFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Device(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DeviceFailedNoDevicesLoaded()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "Run MAP or GET first to load your devices.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);

        //act
        var actualResult = blinkLib.Device(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DeviceFailedDeviceNotFound()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "No single device named 'Unknown Camera' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Device(new[] { "nUnknown Camera" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DeviceSuccessful_Camera()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var expected = MockData.CreateValidHomeScreenCameras()[0]
            .ToDeviceDetailString(MockData.CreateValidHomeScreenNetworks()[0]);

        //act
        var actualResult = blinkLib.Device(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DeviceSuccessful_Doorbell()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var expected = MockData.CreateValidHomeScreenDoorbells()[0]
            .ToDeviceDetailString(MockData.CreateValidHomeScreenNetworks()[0]);

        //act
        var actualResult = blinkLib.Device(new[] { "nFront door bell" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_ImageFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Image(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_ImageFailedNoDevicesLoaded()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "Run MAP or GET first to load your devices.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);

        //act
        var actualResult = blinkLib.Image(new[] { "nPergoda" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_ImageFailedDeviceNotFound()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var expected = "No single device named 'Unknown Camera' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Image(new[] { "nUnknown Camera" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_ImageFailedNoThumbnailYet()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "'NoThumbCam' has no image yet — run SNAP -nNoThumbCam first.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Image(new[] { "nNoThumbCam" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_ImageSuccessful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            //act
            var actualResult = blinkLib.Image(new[] { "nPergoda" }, mediaDirectory);

            //assert
            actualResult.Should().StartWith("Saved image to ");
            var path = actualResult["Saved image to ".Length..].TrimEnd('.');
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(mediaDirectory))
                Directory.Delete(mediaDirectory, recursive: true);
        }
    }

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
    public void Test_LoginRequiresTwoFactor_ThenVerifySucceeds()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[]
        {
            new string("utwofactor@email.com"),
            new string("pPAssword"),
        };
        var verifyArguments = new[]
        {
            new string("v987654"),
        };

        //act
        var blinkLib = new BlinkLibrary(mockAdapter);
        var loginResult = blinkLib.Login(loginArguments);
        var verifyResult = blinkLib.VerifyPin(verifyArguments);

        //assert
        loginResult.Should().Contain("failed");
        loginResult.Should().Contain("two-factor");
        verifyResult.Should().BeEquivalentTo("Login to the Blink Service successful.");

        //Results
        output.WriteLine("LoginMessage: {0}", loginResult);
        output.WriteLine("VerifyMessage: {0}", verifyResult);
    }

    [Fact]
    public void Test_LoginRequiresTwoFactor_ThenVerifyFailsWithWrongCode()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[]
        {
            new string("utwofactor@email.com"),
            new string("pPAssword"),
        };
        var verifyArguments = new[]
        {
            new string("v000000"),
        };

        //act
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        var verifyResult = blinkLib.VerifyPin(verifyArguments);

        //assert
        verifyResult.Should().Contain("failed");

        //Results
        output.WriteLine("VerifyMessage: {0}", verifyResult);
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