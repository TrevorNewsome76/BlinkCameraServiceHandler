using BlinkCameraCall;
using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.System;
using Dependency;
using FluentAssertions;
using Shadow.Quack;
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
    public void Test_SnapFailedCameraAndDoorbellShareName()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "No single device named 'Shared Name Device' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Snap(new[] { "nShared Name Device" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_SnapFailedBlinkRejected()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var act = () => blinkLib.Snap(new[] { "nRejected Snap Cam" });

        //assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*system busy*");
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
    public void Test_ArmFailedNotLoggedIn()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var actualResult = new BlinkLibrary(mockAdapter).Arm("Pergoda");
        actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
    }

    [Fact]
    public void Test_ArmFailedNoDevicesLoaded()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        var actualResult = blinkLib.Arm("Pergoda");
        actualResult.Should().BeEquivalentTo("Run MAP or GET first to load your devices.");
    }

    [Fact]
    public void Test_ArmFailedDeviceNotFound()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var actualResult = blinkLib.Arm("Unknown Camera");
        actualResult.Should().BeEquivalentTo("No single device named 'Unknown Camera' was found. Run MAP to see exact names.");
    }

    [Fact]
    public void Test_ArmFailedCameraAndDoorbellShareName()
    {
        // Reuses the existing ambiguous-name fixture Test_SnapFailedCameraAndDoorbellShareName
        // already relies on, proving device-level Arm treats a name matching both a camera
        // and a doorbell as not-found, the same way Snap already does.
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "unothumbnail@email.com", "pPAssword" });
        blinkLib.Map();
        var actualResult = blinkLib.Arm("Shared Name Device");
        actualResult.Should().BeEquivalentTo("No single device named 'Shared Name Device' was found. Run MAP to see exact names.");
    }

    [Fact]
    public void Test_ArmSuccessful_Camera()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();
        var callsBeforeArm = apiMethods.HomeScreenCallCount;

        var actualResult = blinkLib.Arm("Pergoda", TimeSpan.Zero, 1);

        actualResult.Should().BeEquivalentTo("Armed 'Pergoda'.");
        apiMethods.HomeScreenCallCount.Should().Be(callsBeforeArm + 1);
    }

    [Fact]
    public void Test_ArmSuccessful_RealWorldRichResponseShape_NoLongerMisreportedAsRejected()
    {
        // Reproduces the exact bug this whole plan exists to fix: before the fix,
        // Blink's real (much richer than any prior mock) acceptance response made
        // this throw "Blink rejected the request" even though the command had
        // actually been accepted.
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        var camera = Duck.Implement<IHomeScreenCamera>(new { id = 700005L, Name = "Real Shape Camera", Network_Id = 700000L });
        blinkLib.CurrentSession!.Cameras = new List<IHomeScreenCamera> { camera };

        var actualResult = blinkLib.Arm("Real Shape Camera", TimeSpan.Zero, 1);

        actualResult.Should().Be("Armed 'Real Shape Camera'.");
    }

    [Fact]
    public void Test_ArmSuccessful_Doorbell()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();

        var actualResult = blinkLib.Arm("Front door bell", TimeSpan.Zero, 1);

        actualResult.Should().BeEquivalentTo("Armed 'Front door bell'.");
    }

    [Fact]
    public void Test_ArmFailed_Timeout()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();

        var actualResult = blinkLib.Arm("Garage", TimeSpan.Zero, 1);

        actualResult.Should().BeEquivalentTo("Blink did not finish arming 'Garage' in time.");
    }

    [Fact]
    public void Test_DisarmFailedRejectedOutright()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();

        Assert.Throws<InvalidOperationException>(() => blinkLib.Disarm("Garage", TimeSpan.Zero, 1));
    }

    [Fact]
    public void Test_ArmScoped_CrossHubNameCollision_ResolvesOnlyWithinGivenNetwork()
    {
        // Two different hubs each have a camera named "Pergoda" (one really is the "Home
        // Front" Pergoda fixture at id 321343; the other is a hand-built duplicate living in
        // "Home Back", id 999001). Passing networkId: 263178L must resolve and act on the
        // "Home Front" one specifically, proving device-level Arm no longer searches the whole
        // account once scoped to a hub (Finding 1).
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        blinkLib.CurrentSession!.Cameras = Duck.Implement<List<IHomeScreenCamera>>(new[]
        {
            new { id = 321343L, Name = "Pergoda", Enabled = true, Status = "done", Battery = "ok", Network_Id = 263178L, Thumbnail = "" },
            new { id = 999001L, Name = "Pergoda", Enabled = true, Status = "done", Battery = "ok", Network_Id = 263310L, Thumbnail = "" },
        });

        var actualResult = blinkLib.Arm("Pergoda", TimeSpan.Zero, 1, networkId: 263178L);

        actualResult.Should().BeEquivalentTo("Armed 'Pergoda'.");
    }

    [Fact]
    public void Test_ArmUnscoped_CrossHubNameCollision_IsAmbiguousAndNotFound()
    {
        // Same two-hub "Pergoda" collision as above, but called the old way with no networkId.
        // DeviceLookupExtension's FindCameraByName (via SingleOrNull) searches the whole
        // account, so two same-named cameras in different hubs now collapse to "not found"
        // rather than silently resolving to whichever one happened to match first.
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        blinkLib.CurrentSession!.Cameras = Duck.Implement<List<IHomeScreenCamera>>(new[]
        {
            new { id = 321343L, Name = "Pergoda", Enabled = true, Status = "done", Battery = "ok", Network_Id = 263178L, Thumbnail = "" },
            new { id = 999001L, Name = "Pergoda", Enabled = true, Status = "done", Battery = "ok", Network_Id = 263310L, Thumbnail = "" },
        });

        var actualResult = blinkLib.Arm("Pergoda", TimeSpan.Zero, 1);

        actualResult.Should().BeEquivalentTo("No single device named 'Pergoda' was found. Run MAP to see exact names.");
    }

    [Fact]
    public void Test_ArmSuccessful_Camera_RefreshFailureAppendsLineWithoutDiscardingReport()
    {
        // If the end-of-operation RefreshDeviceLists() call throws (e.g. a network blip on the
        // refresh GetHomeScreen call), the already-collected "Armed '...'." report must still
        // come back, with one extra line describing the refresh failure appended — not lost
        // entirely (Finding 3).
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var apiMethods = (MockHttpClientApiHandler)Dependency.Shelf.RetrieveInstance<IApiMethods>();
        apiMethods.ThrowOnHomeScreen = true;

        var actualResult = blinkLib.Arm("Pergoda", TimeSpan.Zero, 1);

        actualResult.Should().Be(
            "Armed 'Pergoda'." + Environment.NewLine +
            "Could not refresh device state: Simulated network failure. Run MAP.");
    }

    [Fact]
    public void Test_ArmHub_MixedOutcome_ContinuesAndReportsEveryItem()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var homeFront = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Front");
        var expected =
            "Hub 'Home Front': armed." + Environment.NewLine +
            "  Camera 'Pergoda': armed." + Environment.NewLine +
            "  Camera 'Garage': Blink did not finish arming 'Garage' in time." + Environment.NewLine +
            "  Doorbell 'Front door bell': armed." + Environment.NewLine +
            "Armed hub 'Home Front' (3/4 succeeded).";

        var actualResult = blinkLib.Arm(homeFront, TimeSpan.Zero, 1);

        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DisarmHub_NetworkItselfRejected_StillCompletesWithoutThrowing()
    {
        // "Home Back" (263310) has zero child devices, so this isolates the network's OWN
        // call failing (as opposed to Test_ArmHub_MixedOutcome's child-level failure above) —
        // its disarm fixture returns no usable id, proving the network item is caught by the
        // same per-item try/catch as every child, not special-cased out of "continue and report".
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var homeBack = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Back");
        var expected =
            "Hub 'Home Back': Blink rejected the request: {\"message\":\"system busy\"}" + Environment.NewLine +
            "Disarmed hub 'Home Back' (0/1 succeeded).";

        var actualResult = blinkLib.Disarm(homeBack, TimeSpan.Zero, 1);

        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_DisarmHub_NetworkItselfRejected_WithRealChildren_StillAttemptsAll()
    {
        // Unlike Test_DisarmHub_NetworkItselfRejected_StillCompletesWithoutThrowing above
        // (which uses zero-child "Home Back" and so only vacuously proves children "still get
        // attempted"), this uses "Home Front" with its real mixed-outcome children, so the
        // network's own failure and the children's independent outcomes are both exercised
        // and asserted together in one combined scenario.
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var homeFront = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Front");
        var expected =
            "Hub 'Home Front': Blink rejected the request: {\"message\":\"system busy\"}" + Environment.NewLine +
            "  Camera 'Pergoda': disarmed." + Environment.NewLine +
            "  Camera 'Garage': Blink rejected the request: {\"message\":\"system busy\"}" + Environment.NewLine +
            "  Doorbell 'Front door bell': disarmed." + Environment.NewLine +
            "Disarmed hub 'Home Front' (2/4 succeeded).";

        var actualResult = blinkLib.Disarm(homeFront, TimeSpan.Zero, 1);

        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ArmHub_NoChildDevices_SucceedsWithOneOfOne()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var homeBack = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Back");
        var expected =
            "Hub 'Home Back': armed." + Environment.NewLine +
            "Armed hub 'Home Back' (1/1 succeeded).";

        var actualResult = blinkLib.Arm(homeBack, TimeSpan.Zero, 1);

        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ArmHub_NullCamerasAndDoorbells_DoesNotThrow()
    {
        // _sessionDetails.Cameras/Doorbells are null until Map()/Get() first populates them
        // (BlinkLibrary's own Initialize() default). Hub-level Arm takes the network object
        // directly rather than reading it from _sessionDetails.Networks, so it can be called
        // with Cameras/Doorbells still at that default null — proving the cascading loop
        // guards both with `?? new List<...>()` rather than assuming Map() already ran.
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        var homeFront = Duck.Implement<IHomeScreenNetwork>(new { Id = 263178L, Name = "Home Front", Armed = false });

        var act = () => blinkLib.Arm(homeFront, TimeSpan.Zero, 1);

        act.Should().NotThrow();
    }

    [Fact]
    public void Test_ArmHubFailedNotLoggedIn()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var homeFront = blinkLib.CurrentSession!.Networks!.Single(n => n.Name == "Home Front");
        blinkLib.Logout();

        var actualResult = blinkLib.Arm(homeFront, TimeSpan.Zero, 1);

        actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
    }

    [Fact]
    public void Test_ArmSystemFailedNotLoggedIn()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var actualResult = new BlinkLibrary(mockAdapter).Arm();
        actualResult.Should().BeEquivalentTo("Not currently logged into Blink service.");
    }

    [Fact]
    public void Test_ArmSystemFailedNoHubsLoaded()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        var actualResult = blinkLib.Arm();
        actualResult.Should().BeEquivalentTo("Run MAP or GET first to load your devices.");
    }

    [Fact]
    public void Test_ArmSystem_AcrossBothHubs_ReportsGrandTotal()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.Map();
        var expected =
            "Hub 'Home Front': armed." + Environment.NewLine +
            "  Camera 'Pergoda': armed." + Environment.NewLine +
            "  Camera 'Garage': Blink did not finish arming 'Garage' in time." + Environment.NewLine +
            "  Doorbell 'Front door bell': armed." + Environment.NewLine +
            "Armed hub 'Home Front' (3/4 succeeded)." + Environment.NewLine +
            "Hub 'Home Back': armed." + Environment.NewLine +
            "Armed hub 'Home Back' (1/1 succeeded)." + Environment.NewLine +
            "Armed the system (4/5 succeeded across 2 hubs).";

        var actualResult = blinkLib.Arm(TimeSpan.Zero, 1);

        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ArmSystem_NoHubs_ReportsNothingToArm()
    {
        // Networks is a public settable property and CurrentSession exposes the live,
        // already-logged-in session instance, so an empty hub list can be set directly
        // without going through Map() or any new mock fixture.
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.CurrentSession!.Networks = new List<IHomeScreenNetwork>();

        var actualResult = blinkLib.Arm();

        actualResult.Should().BeEquivalentTo("No hubs to arm.");
    }

    [Fact]
    public void Test_DisarmSystem_NoHubs_ReportsNothingToDisarm()
    {
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });
        blinkLib.CurrentSession!.Networks = new List<IHomeScreenNetwork>();

        var actualResult = blinkLib.Disarm();

        actualResult.Should().BeEquivalentTo("No hubs to disarm.");
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
    public void Test_ImageFailedCameraAndDoorbellShareName()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "No single device named 'Shared Name Device' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.Image(new[] { "nShared Name Device" });

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
    public void Test_VideosFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Videos(Array.Empty<string>());

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_VideosSuccessful_PopulatesCache()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        var expected =
            "[1] Id 3001  Device: Pergoda  Recorded: 2026-11-01 12:00:00 UTC" + Environment.NewLine +
            "[2] Id 3002  Device: Garage  Recorded: 2026-11-01 13:00:00 UTC";

        //act
        var actualResult = blinkLib.Videos(Array.Empty<string>());

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_VideosSuccessful_NoEvents()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        var expected = "No cloud video events in the last 24 hours.";

        //act
        var actualResult = blinkLib.Videos(Array.Empty<string>());

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DownloadFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Download(new[] { "i3001" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DownloadFailedNoCachedList()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        var expected = "Run VIDEOS first, or '3001' wasn't in the last VIDEOS list.";

        //act
        var actualResult = blinkLib.Download(new[] { "i3001" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DownloadFailedIdNotInCachedList()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Videos(Array.Empty<string>());
        var expected = "Run VIDEOS first, or '9999' wasn't in the last VIDEOS list.";

        //act
        var actualResult = blinkLib.Download(new[] { "i9999" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_DownloadSuccessful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Videos(Array.Empty<string>());
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            //act
            var actualResult = blinkLib.Download(new[] { "i3001" }, mediaDirectory);

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
    public void Test_LocalClipsFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).LocalClips(new[] { "nActive Sync" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalClipsFailedNoDevicesLoaded()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "Run MAP or GET first to load your devices.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);

        //act
        var actualResult = blinkLib.LocalClips(new[] { "nActive Sync" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalClipsFailedSyncModuleNotFound()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "No single device named 'Unknown Sync' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.LocalClips(new[] { "nUnknown Sync" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalClipsFailedNotActive()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "'Inactive Sync' doesn't have active local storage.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.LocalClips(new[] { "nInactive Sync" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalClipsSuccessful_PopulatesCache()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var expected = "[1] Id 1  Camera: Pergoda  Recorded: 2026-11-01 12:00:00 UTC  Size: 12345 bytes";

        //act
        var actualResult = blinkLib.LocalClips(new[] { "nActive Sync" }, TimeSpan.Zero, 3);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalClipsSuccessful_EmptyManifest()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var expected = "No clips on 'Empty Sync''s SD card.";

        //act
        var actualResult = blinkLib.LocalClips(new[] { "nEmpty Sync" }, TimeSpan.Zero, 3);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalClipsFailedBlinkRejected()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var act = () => blinkLib.LocalClips(new[] { "nRejected Manifest Sync" });

        //assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*system busy*");
    }

    [Fact]
    public void Test_LocalDownloadFailedBlinkRejected()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        blinkLib.LocalClips(new[] { "nReject Prepare Sync" });

        //act
        var act = () => blinkLib.LocalDownload(new[] { "nReject Prepare Sync", "i4" });

        //assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*system busy*");
    }

    [Fact]
    public void Test_LocalClipsTimedOut()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var expected = "Blink did not finish reading the SD-card manifest for 'Timeout Sync' in time.";

        //act
        var actualResult = blinkLib.LocalClips(new[] { "nTimeout Sync" }, TimeSpan.Zero, 2);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalDownloadFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).LocalDownload(new[] { "nActive Sync", "i1" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalDownloadFailedSyncModuleNotFound()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "No single device named 'Unknown Sync' was found. Run MAP to see exact names.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.LocalDownload(new[] { "nUnknown Sync", "i1" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalDownloadFailedNoCachedManifest()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "Run LOCALCLIPS -nActive Sync first.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();

        //act
        var actualResult = blinkLib.LocalDownload(new[] { "nActive Sync", "i1" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalDownloadFailedSyncModuleMismatch()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "Run LOCALCLIPS -nActive Sync first.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        blinkLib.LocalClips(new[] { "nEmpty Sync" }, TimeSpan.Zero, 3);

        //act
        var actualResult = blinkLib.LocalDownload(new[] { "nActive Sync", "i1" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalDownloadFailedClipIdNotInManifest()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "'9999' wasn't in the last LOCALCLIPS list for 'Active Sync'.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        blinkLib.LocalClips(new[] { "nActive Sync" }, TimeSpan.Zero, 3);

        //act
        var actualResult = blinkLib.LocalDownload(new[] { "nActive Sync", "i9999" });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_LocalDownloadSuccessful()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        blinkLib.LocalClips(new[] { "nActive Sync" }, TimeSpan.Zero, 3);
        var mediaDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            //act
            var actualResult = blinkLib.LocalDownload(new[] { "nActive Sync", "i1" }, TimeSpan.Zero, 3, mediaDirectory);

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
    public void Test_LocalDownloadTimedOut()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateNoThumbnailSettings());
        var loginArguments = new[] { "unothumbnail@email.com", "pPAssword" };
        var expected = "Blink did not finish preparing the clip for 'Download Timeout Sync' in time.";
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        blinkLib.LocalClips(new[] { "nDownload Timeout Sync" }, TimeSpan.Zero, 3);

        //act
        var actualResult = blinkLib.LocalDownload(new[] { "nDownload Timeout Sync", "i2" }, TimeSpan.Zero, 2);

        //assert
        actualResult.Should().BeEquivalentTo(expected);
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
    public void Test_IsLoggedIn_FalseBeforeLogin()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);

        //assert
        blinkLib.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public void Test_IsLoggedIn_TrueAfterSuccessfulLogin()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);

        //act
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });

        //assert
        blinkLib.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public void Test_TwoFactorPending_FalseBeforeLogin()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);

        //assert
        blinkLib.TwoFactorPending.Should().BeFalse();
    }

    [Fact]
    public void Test_TwoFactorPending_TrueWhenLoginRequiresTwoFactor()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);

        //act
        blinkLib.Login(new[] { "utwofactor@email.com", "pPAssword" });

        //assert
        blinkLib.TwoFactorPending.Should().BeTrue();
    }

    [Fact]
    public void Test_TwoFactorPending_FalseAfterSuccessfulLogin()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);

        //act
        blinkLib.Login(new[] { "utest@email.com", "pPAssword" });

        //assert
        blinkLib.TwoFactorPending.Should().BeFalse();
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

    [Fact]
    public void Test_CurrentSession_NotLoggedIn_ReturnsNull()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var blinkLib = new BlinkLibrary(mockAdapter);

        //act
        var actualResult = blinkLib.CurrentSession;

        //assert
        actualResult.Should().BeNull();
    }

    [Fact]
    public void Test_CurrentSession_LoggedIn_ReturnsSessionDetails()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);

        //act
        var actualResult = blinkLib.CurrentSession;

        //assert
        actualResult.Should().NotBeNull();
        actualResult!.LoggedInStatus.Should().BeTrue();
    }

    [Fact]
    public void Test_ListDeviceMedia_MergesCurrentCloudAndLocal_DeduplicatesCollidingNames()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var camera = MockData.CreateValidHomeScreenCameras()[0];

        //act
        var result = blinkLib.ListDeviceMedia("Pergoda", camera, null, TimeSpan.Zero, 3);

        //assert
        result.Entries.Should().HaveCount(4);
        result.Entries.Should().Contain(e => e.Name == "current" && e.Kind == DeviceMediaKind.Current);
        result.Entries.Should().Contain(e => e.Name == "2026-11-01_12-00-00" && e.Kind == DeviceMediaKind.Cloud);
        result.Entries.Should().Contain(e => e.Name == "2026-11-02_08-00-00" && e.Kind == DeviceMediaKind.Local);
        result.Entries.Should().Contain(e => e.Name == "2026-11-01_12-00-00_12" && e.Kind == DeviceMediaKind.Local);
        result.Entries.Should().NotContain(e => e.LocalClip != null && e.LocalClip.Camera_Name == "Garage");
    }

    [Fact]
    public void Test_ListDeviceMedia_NoThumbnail_NoCloudMatch_NoSyncModuleForNetwork_ReturnsEmptyEntries()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var orphanCamera = Duck.Implement<IHomeScreenCamera>(new { id = 99999L, Name = "Orphan Cam", Network_Id = 555555L, Thumbnail = "" });

        //act
        var result = blinkLib.ListDeviceMedia("Orphan Cam", orphanCamera, null, TimeSpan.Zero, 3);

        //assert
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Test_ListDeviceMedia_SyncModuleInactive_SkipsLocalSection_ButKeepsCurrent()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Map();
        var backCamera = Duck.Implement<IHomeScreenCamera>(new { id = 99998L, Name = "Back Cam", Network_Id = 263310L, Thumbnail = "/some/thumb.jpg" });

        //act
        var result = blinkLib.ListDeviceMedia("Back Cam", backCamera, null, TimeSpan.Zero, 3);

        //assert
        result.Entries.Should().ContainSingle(e => e.Name == "current" && e.Kind == DeviceMediaKind.Current);
    }

    [Fact]
    public void Test_WatchFailedNotLoggedIn()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently logged into Blink service.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Watch(Array.Empty<string>(), _ => { });

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_WatchSuccessful_ReturnsStatusMessage()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        var expected = "Watching for new events every 20s. Use UNWATCH to stop.";

        //act
        var actualResult = blinkLib.Watch(Array.Empty<string>(), _ => { });
        blinkLib.Unwatch();

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_WatchFailed_AlreadyWatching()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Watch(Array.Empty<string>(), _ => { });
        var expected = "Already watching. Use UNWATCH to stop.";

        //act
        var actualResult = blinkLib.Watch(Array.Empty<string>(), _ => { });
        blinkLib.Unwatch();

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_UnwatchFailedNotWatching()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var expected = "Not currently watching.";

        //act
        var actualResult = new BlinkLibrary(mockAdapter).Unwatch();

        //assert
        actualResult.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Test_UnwatchSuccessful_AllowsRestart()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Watch(Array.Empty<string>(), _ => { });
        var expected = "Stopped watching.";

        //act
        var actualResult = blinkLib.Unwatch();
        var restartResult = blinkLib.Watch(Array.Empty<string>(), _ => { });
        blinkLib.Unwatch();

        //assert
        actualResult.Should().BeEquivalentTo(expected);
        restartResult.Should().BeEquivalentTo("Watching for new events every 20s. Use UNWATCH to stop.");
    }

    [Fact]
    public void Test_LogoutWhileWatching_StopsWatch_SoNextLoginCanWatchAgain()
    {
        //assign
        var mockAdapter = new MockAdapter(MockSettings.CreateSettings());
        var loginArguments = new[] { "utest@email.com", "pPAssword" };
        var blinkLib = new BlinkLibrary(mockAdapter);
        blinkLib.Login(loginArguments);
        blinkLib.Watch(Array.Empty<string>(), _ => { });

        //act
        blinkLib.Logout();
        blinkLib.Login(loginArguments);
        var actualResult = blinkLib.Watch(Array.Empty<string>(), _ => { });
        blinkLib.Unwatch();

        //assert
        actualResult.Should().BeEquivalentTo("Watching for new events every 20s. Use UNWATCH to stop.");
    }
}