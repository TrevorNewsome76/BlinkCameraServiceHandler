using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Extensions;

public static class DeviceMapExtension
{
    public static string ToDeviceMapString(this IGetHomeScreenResponse response)
    {
        var networks = response.Networks ?? new List<IHomeScreenNetwork>();
        var syncModules = response.Sync_Modules ?? new List<IHomeScreenSyncModule>();
        var cameras = response.Cameras ?? new List<IHomeScreenCamera>();
        var doorbells = response.Doorbells ?? new List<IHomeScreenDoorbells>();

        if (networks.Count == 0 && syncModules.Count == 0 && cameras.Count == 0 && doorbells.Count == 0)
            return "No devices found.";

        var networkIds = networks.Select(n => n.Id.ToString()).ToHashSet();
        var longNetworkIds = networks.Select(n => n.Id).ToHashSet();

        var blocks = new List<string>();

        foreach (var network in networks)
        {
            var networkIdString = network.Id.ToString();
            var networkSyncModules = syncModules.Where(s => s.Network_Id == networkIdString);
            var networkCameras = cameras.Where(c => c.Network_Id == network.Id).ToList();
            var networkDoorbells = doorbells.Where(d => d.Network_Id == network.Id).ToList();

            var lines = new List<string>
            {
                network.FormatNetworkLine()
            };
            lines.AddRange(networkSyncModules.Select(FormatSyncModuleLine));

            if (networkCameras.Count == 0 && networkDoorbells.Count == 0)
            {
                lines.Add("  (no cameras or doorbells)");
            }
            else
            {
                lines.AddRange(networkCameras.Select(FormatCameraLine));
                lines.AddRange(networkDoorbells.Select(FormatDoorbellLine));
            }

            blocks.Add(string.Join(Environment.NewLine, lines));
        }

        var unassignedLines = new List<string>();
        unassignedLines.AddRange(syncModules.Where(s => !networkIds.Contains(s.Network_Id)).Select(FormatSyncModuleLine));
        unassignedLines.AddRange(cameras.Where(c => !longNetworkIds.Contains(c.Network_Id)).Select(FormatCameraLine));
        unassignedLines.AddRange(doorbells.Where(d => !longNetworkIds.Contains(d.Network_Id)).Select(FormatDoorbellLine));

        if (unassignedLines.Count > 0)
        {
            unassignedLines.Insert(0, "Unassigned devices:");
            blocks.Add(string.Join(Environment.NewLine, unassignedLines));
        }

        var accessoryLines = new List<string>();
        AddAccessoryLine(accessoryLines, "Owls (Blink Mini cameras)", response.Owls);
        AddAccessoryLine(accessoryLines, "Sirens", response.Sirens);
        AddAccessoryLine(accessoryLines, "Chimes", response.Chimes);
        AddAccessoryLine(accessoryLines, "Doorbell_Buttons", response.Doorbell_Buttons);

        if (accessoryLines.Count > 0)
            blocks.Add(string.Join(Environment.NewLine, accessoryLines));

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }

    private static void AddAccessoryLine(List<string> lines, string name, string[]? items)
    {
        if (items is not null && items.Length > 0)
            lines.Add($"{name}: {items.Length} (not detailed)");
    }

    private static string FormatSyncModuleLine(IHomeScreenSyncModule syncModule) =>
        $"  Sync Module: {syncModule.Name}  [{syncModule.Status}, fw {syncModule.Fw_Version}, wifi {syncModule.Wifi_Strength}]";

    internal static string FormatNetworkLine(this IHomeScreenNetwork network) =>
        $"Network: {network.Name} ({(network.Armed ? "armed" : "disarmed")})";

    internal static string FormatCameraLine(IHomeScreenCamera camera) =>
        $"  Camera: {camera.Name}  [{(camera.Enabled ? "enabled" : "disabled")}, status {camera.Status}, battery {camera.Battery}, wifi {FormatWifi(camera.Signals)}]";

    internal static string FormatDoorbellLine(IHomeScreenDoorbells doorbell) =>
        $"  Doorbell: {doorbell.Name}  [{(doorbell.Enabled ? "enabled" : "disabled")}, status {doorbell.Status}, battery {doorbell.Battery}, wifi {FormatWifi(doorbell.Signals)}]";

    private static string FormatWifi(IHomeScreenCameraSignals? signals) =>
        signals is null ? "n/a" : signals.Wifi.ToString();
}
