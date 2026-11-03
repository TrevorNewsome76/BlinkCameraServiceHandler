using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_DeviceDetailExtension
{
    [Fact]
    public void Test_ToDeviceDetailString_Camera_FormatsAllFields()
    {
        var camera = MockData.CreateValidHomeScreenCameras()[0];
        var network = MockData.CreateValidHomeScreenNetworks()[0];

        var expected =
            "Camera: Pergoda" + Environment.NewLine +
            "  Network:        Home Front (disarmed)" + Environment.NewLine +
            "  Status:         done" + Environment.NewLine +
            "  Enabled:        true" + Environment.NewLine +
            "  Battery:        ok" + Environment.NewLine +
            "  Wifi signal:    5" + Environment.NewLine +
            "  Firmware:       10.64" + Environment.NewLine +
            "  Serial:         G8T1GJ0120430Q9H" + Environment.NewLine +
            "  Local storage:  disabled, compatible" + Environment.NewLine +
            "  Thumbnail:      /api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg?ts=1705051738&ext= (run IMAGE to download it)";

        camera.ToDeviceDetailString(network).Should().Be(expected);
    }

    [Fact]
    public void Test_ToDeviceDetailString_Doorbell_FormatsAllFields()
    {
        var doorbell = MockData.CreateValidHomeScreenDoorbells()[0];
        var network = MockData.CreateValidHomeScreenNetworks()[0];

        var expected =
            "Doorbell: Front door bell" + Environment.NewLine +
            "  Network:        Home Front (disarmed)" + Environment.NewLine +
            "  Status:         done" + Environment.NewLine +
            "  Enabled:        true" + Environment.NewLine +
            "  Battery:        ok" + Environment.NewLine +
            "  Wifi signal:    5" + Environment.NewLine +
            "  Firmware:       12.66" + Environment.NewLine +
            "  Serial:         G8T1TV0220752BKL" + Environment.NewLine +
            "  Local storage:  disabled, incompatible" + Environment.NewLine +
            "  Doorbell mode:  lfr" + Environment.NewLine +
            "  Thumbnail:      /api/v3/media/accounts/239361/networks/263178/lotus/5009/thumbnail/thumbnail.jpg?ts=1705051747&ext= (run IMAGE to download it)";

        doorbell.ToDeviceDetailString(network).Should().Be(expected);
    }
}
