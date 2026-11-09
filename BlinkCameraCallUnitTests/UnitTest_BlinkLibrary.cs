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