using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Extensions;

public static class DeviceDetailExtension
{
    public static string ToDeviceDetailString(this IHomeScreenCamera camera, IHomeScreenNetwork network) =>
        string.Join(Environment.NewLine, new[]
        {
            $"Camera: {camera.Name}",
            NetworkLine(network),
            $"  Status:         {camera.Status}",
            $"  Enabled:        {FormatBool(camera.Enabled)}",
            $"  Battery:        {camera.Battery}",
            $"  Wifi signal:    {FormatWifi(camera.Signals)}",
            $"  Firmware:       {camera.Fw_Version}",
            $"  Serial:         {camera.Serial}",
            LocalStorageLine(camera.Local_Storage_Enabled, camera.Local_Storage_Compatible),
            ThumbnailLine(camera.Thumbnail),
        });

    public static string ToDeviceDetailString(this IHomeScreenDoorbells doorbell, IHomeScreenNetwork network) =>
        string.Join(Environment.NewLine, new[]
        {
            $"Doorbell: {doorbell.Name}",
            NetworkLine(network),
            $"  Status:         {doorbell.Status}",
            $"  Enabled:        {FormatBool(doorbell.Enabled)}",
            $"  Battery:        {doorbell.Battery}",
            $"  Wifi signal:    {FormatWifi(doorbell.Signals)}",
            $"  Firmware:       {doorbell.Fw_Version}",
            $"  Serial:         {doorbell.Serial}",
            LocalStorageLine(doorbell.Local_Storage_Enabled, doorbell.Local_Storage_Compatible),
            $"  Doorbell mode:  {doorbell.Doorbell_Mode}",
            ThumbnailLine(doorbell.Thumbnail),
        });

    private static string NetworkLine(IHomeScreenNetwork network) =>
        $"  Network:        {network.Name} ({(network.Armed ? "armed" : "disarmed")})";

    private static string LocalStorageLine(bool enabled, bool compatible) =>
        $"  Local storage:  {(enabled ? "enabled" : "disabled")}, {(compatible ? "compatible" : "incompatible")}";

    private static string ThumbnailLine(string thumbnail) =>
        string.IsNullOrEmpty(thumbnail)
            ? "  Thumbnail:      none yet (run SNAP to capture one)"
            : $"  Thumbnail:      {thumbnail} (run IMAGE to download it)";

    private static string FormatWifi(IHomeScreenCameraSignals? signals) =>
        signals is null ? "n/a" : signals.Wifi.ToString();

    private static string FormatBool(bool value) => value ? "true" : "false";
}
