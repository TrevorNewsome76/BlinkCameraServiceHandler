using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces.System;
using FluentAssertions;
using Shadow.Quack;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_DeviceMapExtension
{
    [Fact]
    public void Test_FullHomeScreen_ProducesGroupedTree()
    {
        var response = MockData.CreateValidHomeScreenResponse();

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

        response.ToDeviceMapString().Should().Be(expected);
    }

    [Fact]
    public void Test_UnassignedDevice_ListedInTrailingSection()
    {
        var response = Duck.Implement<IGetHomeScreenResponse>(new
        {
            Networks = Duck.Implement<List<IHomeScreenNetwork>>(new[]
            {
                new { Id = 1L, Name = "Only Network", Armed = false }
            }),
            Sync_Modules = Duck.Implement<List<IHomeScreenSyncModule>>(new[]
            {
                new { Id = 2L, Name = "Orphan Sync", Status = "online", Fw_Version = "1.0", Wifi_Strength = 3, Network_Id = "999" }
            }),
            Cameras = Duck.Implement<List<IHomeScreenCamera>>(Array.Empty<object>()),
            Doorbells = Duck.Implement<List<IHomeScreenDoorbells>>(Array.Empty<object>()),
        });

        var expected =
            "Network: Only Network (disarmed)" + Environment.NewLine +
            "  (no cameras or doorbells)" + Environment.NewLine +
            Environment.NewLine +
            "Unassigned devices:" + Environment.NewLine +
            "  Sync Module: Orphan Sync  [online, fw 1.0, wifi 3]";

        response.ToDeviceMapString().Should().Be(expected);
    }

    [Fact]
    public void Test_NullSignalsAndNoResponse_DoNotThrow()
    {
        var response = Duck.Implement<IGetHomeScreenResponse>(new
        {
            Networks = Duck.Implement<List<IHomeScreenNetwork>>(new[] { new { Id = 1L, Name = "N", Armed = true } }),
            Cameras = Duck.Implement<List<IHomeScreenCamera>>(new[]
            {
                new { id = 2L, Name = "NoSignalCam", Enabled = true, Status = "done", Battery = "ok", Network_Id = 1L }
            }),
        });

        var expected =
            "Network: N (armed)" + Environment.NewLine +
            "  Camera: NoSignalCam  [enabled, status done, battery ok, wifi n/a]";

        response.ToDeviceMapString().Should().Be(expected);

        Duck.Implement<IGetHomeScreenResponse>(new()).ToDeviceMapString().Should().Be("No devices found.");
    }

    [Fact]
    public void Test_NoNetworks_DevicesStillReportedAsUnassigned()
    {
        var response = Duck.Implement<IGetHomeScreenResponse>(new
        {
            Cameras = Duck.Implement<List<IHomeScreenCamera>>(new[]
            {
                new { id = 1L, Name = "Orphan Camera", Enabled = true, Status = "done", Battery = "ok", Network_Id = 99L }
            }),
        });

        var expected =
            "Unassigned devices:" + Environment.NewLine +
            "  Camera: Orphan Camera  [enabled, status done, battery ok, wifi n/a]";

        response.ToDeviceMapString().Should().Be(expected);
    }

    [Fact]
    public void Test_NonEmptyAccessoryArrays_AppendCountLines()
    {
        var response = Duck.Implement<IGetHomeScreenResponse>(new
        {
            Networks = Duck.Implement<List<IHomeScreenNetwork>>(new[] { new { Id = 1L, Name = "N", Armed = false } }),
            Owls = new[] { "owl-1", "owl-2" },
            Sirens = new[] { "siren-1" },
        });

        var expected =
            "Network: N (disarmed)" + Environment.NewLine +
            "  (no cameras or doorbells)" + Environment.NewLine +
            Environment.NewLine +
            "Owls (Blink Mini cameras): 2 (not detailed)" + Environment.NewLine +
            "Sirens: 1 (not detailed)";

        response.ToDeviceMapString().Should().Be(expected);
    }
}
