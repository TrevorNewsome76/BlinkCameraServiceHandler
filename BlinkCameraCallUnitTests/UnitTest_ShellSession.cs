using BlinkCameraCall;
using BlinkCommon.Interfaces;
using Dependency;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests;

[Collection("Sequential")]
public class UnitTest_ShellSession
{
    private static readonly string[] LoginArguments = { "utest@email.com", "pPAssword" };

    [Fact]
    public void Test_Prompt_StartsAtRoot()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var shellSession = new ShellSession(new BlinkLibrary(mockAdapter));

        //act
        var actualResult = shellSession.Prompt;

        //assert
        actualResult.Should().Be("BC>");
    }

    [Fact]
    public void Test_ChangeDirectory_NotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var shellSession = new ShellSession(new BlinkLibrary(mockAdapter));
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = shellSession.ChangeDirectory("Home Front");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ChangeDirectory_ToHub_NoMapRun()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        var shellSession = new ShellSession(blinkLib);
        var expected = "Run MAP or GET first to load your devices.";

        //act
        var actualResult = shellSession.ChangeDirectory("Home Front");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ChangeDirectory_ToHub_Successful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);

        //act
        var actualResult = shellSession.ChangeDirectory("Home Front");

        //assert
        actualResult.Should().Be("Now in hub 'Home Front'.");
        shellSession.Prompt.Should().Be("BC\\Home Front>");
    }

    [Fact]
    public void Test_ChangeDirectory_ToHub_UnknownName()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        var expected = "No hub named 'Nope' was found. Run LS to see exact names.";

        //act
        var actualResult = shellSession.ChangeDirectory("Nope");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ChangeDirectory_ToDevice_Successful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");

        //act
        var actualResult = shellSession.ChangeDirectory("Pergoda");

        //assert
        actualResult.Should().Be("Now in device 'Pergoda'.");
        shellSession.Prompt.Should().Be("BC\\Home Front\\Pergoda>");
    }

    [Fact]
    public void Test_ChangeDirectory_ToDevice_UnknownName()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        var expected = "No device named 'Nope' was found. Run LS to see exact names.";

        //act
        var actualResult = shellSession.ChangeDirectory("Nope");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ChangeDirectory_DotDot_FromDeviceToHub()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");

        //act
        var actualResult = shellSession.ChangeDirectory("..");

        //assert
        actualResult.Should().Be("Now in hub 'Home Front'.");
        shellSession.Prompt.Should().Be("BC\\Home Front>");
    }

    [Fact]
    public void Test_ChangeDirectory_DotDot_FromHubToRoot()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");

        //act
        var actualResult = shellSession.ChangeDirectory("..");

        //assert
        actualResult.Should().Be("Back to root.");
        shellSession.Prompt.Should().Be("BC>");
    }

    [Fact]
    public void Test_ChangeDirectory_DotDot_AtRoot_IsNoOp()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);

        //act
        var actualResult = shellSession.ChangeDirectory("..");

        //assert
        actualResult.Should().Be("Already at root.");
        shellSession.Prompt.Should().Be("BC>");
    }

    [Fact]
    public void Test_ChangeDirectory_Backslash_FromDevice_JumpsToRoot()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");

        //act
        var actualResult = shellSession.ChangeDirectory("\\");

        //assert
        actualResult.Should().Be("Back to root.");
        shellSession.Prompt.Should().Be("BC>");
    }

    [Fact]
    public void Test_ChangeDirectory_BareNoTarget_JumpsToRoot()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");

        //act
        var actualResult = shellSession.ChangeDirectory("");

        //assert
        actualResult.Should().Be("Back to root.");
        shellSession.Prompt.Should().Be("BC>");
    }

    [Fact]
    public void Test_ChangeDirectory_FromDevice_AnyTarget_Fails()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        var expected = "No device named 'Garage' was found. Run LS to see exact names.";

        //act
        var actualResult = shellSession.ChangeDirectory("Garage");

        //assert
        actualResult.Should().Be(expected);
        shellSession.Prompt.Should().Be("BC\\Home Front\\Pergoda>");
    }

    [Fact]
    public void Test_List_NotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var shellSession = new ShellSession(new BlinkLibrary(mockAdapter));
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = shellSession.List();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_List_AtRoot_NoMapRun()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        var shellSession = new ShellSession(blinkLib);
        var expected = "Run MAP or GET first to load your devices.";

        //act
        var actualResult = shellSession.List();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_List_AtRoot_ListsHubs()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        var expected =
            "Network: Home Front (disarmed)" + Environment.NewLine +
            "  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "    current  [current]" + Environment.NewLine +
            "    2026-11-01_12-00-00  [cloud]" + Environment.NewLine +
            "  Camera: Garage  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "    current  [current]" + Environment.NewLine +
            "    2026-11-01_13-00-00  [cloud]" + Environment.NewLine +
            "  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "    current  [current]" + Environment.NewLine +
            "Network: Home Back (disarmed)" + Environment.NewLine +
            "  (no cameras or doorbells)";

        //act
        var actualResult = shellSession.List();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_List_AtRoot_OnlyFetchesCloudMediaOnce()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        var apiMethods = (MockHttpClientApiHandler)Shelf.RetrieveInstance<IApiMethods>();

        //act
        shellSession.List();

        //assert
        apiMethods.MediaChangedCallCount.Should().Be(1);
    }

    [Fact]
    public void Test_List_AtHub_ListsDevices()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        var expected =
            "  Camera: Pergoda  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "    current  [current]" + Environment.NewLine +
            "    2026-11-01_12-00-00  [cloud]" + Environment.NewLine +
            "  Camera: Garage  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "    current  [current]" + Environment.NewLine +
            "    2026-11-01_13-00-00  [cloud]" + Environment.NewLine +
            "  Doorbell: Front door bell  [enabled, status done, battery ok, wifi 5]" + Environment.NewLine +
            "    current  [current]";

        //act
        var actualResult = shellSession.List();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_List_AtHub_OnlyFetchesCloudMediaOnce()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        var apiMethods = (MockHttpClientApiHandler)Shelf.RetrieveInstance<IApiMethods>();

        //act
        shellSession.List();

        //assert
        apiMethods.MediaChangedCallCount.Should().Be(1);
    }

    [Fact]
    public void Test_List_AtHub_DevicesWithNoMedia_ShowStatusLineOnlyNoNestedLines()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Solo Network");
        var expected =
            "  Camera: NoThumbCam  [enabled, status done, battery ok, wifi n/a]" + Environment.NewLine +
            "  Camera: Shared Name Device  [enabled, status done, battery ok, wifi n/a]" + Environment.NewLine +
            "  Camera: Rejected Snap Cam  [enabled, status done, battery ok, wifi n/a]" + Environment.NewLine +
            "  Doorbell: Shared Name Device  [enabled, status done, battery ok, wifi n/a]";

        //act
        var actualResult = shellSession.List();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_Trigger_NoCurrentDevice()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        var expected = "Not in a device directory. cd into a camera or doorbell first.";

        //act
        var actualResult = shellSession.Trigger();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_Trigger_Successful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        var expected = "New image captured for 'Pergoda'. Run IMAGE -nPergoda to download it.";

        //act
        var actualResult = shellSession.Trigger(TimeSpan.Zero, 1);

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_List_AtDevice_MergesCurrentCloudAndLocal()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        var expected =
            "current  [current]" + Environment.NewLine +
            "2026-11-02_08-00-00  [local]" + Environment.NewLine +
            "2026-11-01_12-00-00  [cloud]" + Environment.NewLine +
            "2026-11-01_12-00-00_12  [local]";

        //act
        var actualResult = shellSession.List(TimeSpan.Zero, 3);

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DownloadByName_NoCurrentDevice()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        var expected = "Not in a device directory. cd into a camera or doorbell first.";

        //act
        var actualResult = shellSession.DownloadByName("current");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DownloadByName_NoListingYet()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        var expected = "Run LS first.";

        //act
        var actualResult = shellSession.DownloadByName("current");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DownloadByName_SwitchingDeviceInvalidatesCache()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 3);
        shellSession.ChangeDirectory("..");
        shellSession.ChangeDirectory("Garage");
        var expected = "Run LS first.";

        //act
        var actualResult = shellSession.DownloadByName("current");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DownloadByName_NameNotFound()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 3);
        var expected = "'nope' wasn't in the last LS output.";

        //act
        var actualResult = shellSession.DownloadByName("nope");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DownloadByName_Current_Successful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 3);
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            //act
            var actualResult = shellSession.DownloadByName("current", mediaDirectory: mediaDirectory);

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
    public void Test_DownloadByName_Cloud_Successful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 3);
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            //act
            var actualResult = shellSession.DownloadByName("2026-11-01_12-00-00", mediaDirectory: mediaDirectory);

            //assert
            actualResult.Should().StartWith("Saved video to ");
            var path = actualResult["Saved video to ".Length..].TrimEnd('.');
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(mediaDirectory))
                Directory.Delete(mediaDirectory, recursive: true);
        }
    }

    [Fact]
    public void Test_DownloadByName_Local_Successful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 3);
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            //act
            var actualResult = shellSession.DownloadByName("2026-11-02_08-00-00", TimeSpan.Zero, 1, mediaDirectory);

            //assert
            actualResult.Should().StartWith("Saved video to ");
            var path = actualResult["Saved video to ".Length..].TrimEnd('.');
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(mediaDirectory))
                Directory.Delete(mediaDirectory, recursive: true);
        }
    }

    [Fact]
    public void Test_DownloadByName_Current_ReflectsThumbnailAfterTrigger()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 1);
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Trigger() re-fetches the home screen, which (per the mock's HomeScreenCallCount
            // pattern) now returns Pergoda with a new Thumbnail. DownloadByName must resolve the
            // camera fresh instead of using the object cached at cd-time, or this would download
            // the stale thumbnail from before the trigger.
            shellSession.Trigger(TimeSpan.Zero, 1);

            //act
            var actualResult = shellSession.DownloadByName("current", mediaDirectory: mediaDirectory);

            //assert
            actualResult.Should().StartWith("Saved image to ");
            var path = actualResult["Saved image to ".Length..].TrimEnd('.');
            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().Be("fake-jpeg-bytes-updated");
        }
        finally
        {
            if (Directory.Exists(mediaDirectory))
                Directory.Delete(mediaDirectory, recursive: true);
        }
    }

    [Fact]
    public void Test_DownloadByName_NotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        shellSession.List(TimeSpan.Zero, 3);
        blinkLib.Logout();
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = shellSession.DownloadByName("current");

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_Arm_AtRoot_DelegatesToSystemWide()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);

        //act
        var actualResult = shellSession.Arm(TimeSpan.Zero, 1);

        //assert
        actualResult.Should().StartWith("Hub 'Home Front': armed.");
        actualResult.Should().Contain("Armed the system (");
    }

    [Fact]
    public void Test_Arm_InHub_DelegatesToThatHubOnly()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Back");
        var expected =
            "Hub 'Home Back': armed." + Environment.NewLine +
            "Armed hub 'Home Back' (1/1 succeeded).";

        //act
        var actualResult = shellSession.Arm(TimeSpan.Zero, 1);

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_Disarm_InDevice_DelegatesToThatDeviceOnly()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        var expected = "Disarmed 'Pergoda'.";

        //act
        var actualResult = shellSession.Disarm(TimeSpan.Zero, 1);

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_Arm_InDevice_AfterLogout_ReportsNotLoggedIn()
    {
        // ShellSession keeps its own cd state across LOGOUT (it doesn't listen for that event) —
        // Arm must still surface BlinkLibrary's own guard, not crash on a stale reference.
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        blinkLib.Logout();
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = shellSession.Arm(TimeSpan.Zero, 1);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_Arm_InHub_StreamsViaOnProgress()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Back");
        var streamed = new List<string>();

        shellSession.Arm(TimeSpan.Zero, 1, onProgress: streamed.Add);

        streamed.Should().NotBeEmpty();
    }

    [Fact]
    public void Test_Arm_AtRoot_CancelledBeforeCall_ReportsCancelled()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var actualResult = shellSession.Arm(TimeSpan.Zero, 1, cancellationToken: cts.Token);

        actualResult.Should().Contain("cancelled");
    }

    [Fact]
    public void Test_Prompt_ResetsAfterLogout()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(LoginArguments);
        blinkLib.Map();
        var shellSession = new ShellSession(blinkLib);
        shellSession.ChangeDirectory("Home Front");
        shellSession.ChangeDirectory("Pergoda");
        blinkLib.Logout();

        //act
        // List()'s not-logged-in branch is what clears navigation state; Prompt itself doesn't
        // proactively recompute off CurrentSession.
        shellSession.List();

        //assert
        shellSession.Prompt.Should().Be("BC>");
    }
}
