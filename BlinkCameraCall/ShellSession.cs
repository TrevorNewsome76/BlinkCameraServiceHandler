using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall;

public class ShellSession(BlinkLibrary library)
{
    private IHomeScreenNetwork? _currentNetwork;
    private IHomeScreenCamera? _currentCamera;
    private IHomeScreenDoorbells? _currentDoorbell;
    private string? _currentDeviceName;

    private DeviceMediaListing? _lastDeviceMediaListing;
    private string? _lastDeviceMediaListingFor;

    public string Prompt =>
        _currentDeviceName is not null ? $"BC\\{_currentNetwork!.Name}\\{_currentDeviceName}>" :
        _currentNetwork is not null ? $"BC\\{_currentNetwork.Name}>" :
        "BC>";

    public string ChangeDirectory(string target)
    {
        if (library.CurrentSession is null)
            return "Not currently logged into Blink service.";

        var trimmedTarget = target.Trim();

        if (trimmedTarget.Length == 0 || trimmedTarget == "\\" || trimmedTarget == "/")
        {
            ClearAllState();
            return "Back to root.";
        }

        if (trimmedTarget == "..")
        {
            if (_currentDeviceName is not null)
            {
                ClearDeviceState();
                return $"Now in hub '{_currentNetwork!.Name}'.";
            }

            if (_currentNetwork is not null)
            {
                ClearAllState();
                return "Back to root.";
            }

            return "Already at root.";
        }

        if (_currentDeviceName is not null)
            return $"No device named '{target}' was found. Run LS to see exact names.";

        if (library.CurrentSession!.Networks is null)
            return "Run MAP or GET first to load your devices.";

        if (_currentNetwork is null)
        {
            var network = library.CurrentSession.Networks
                .FirstOrDefault(n => string.Equals(n.Name, target, StringComparison.OrdinalIgnoreCase));

            if (network is null)
                return $"No hub named '{target}' was found. Run LS to see exact names.";

            _currentNetwork = network;
            return $"Now in hub '{network.Name}'.";
        }

        var networkCameras = (library.CurrentSession.Cameras ?? new List<IHomeScreenCamera>())
            .Where(c => c.Network_Id == _currentNetwork.Id)
            .ToList();
        var networkDoorbells = (library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>())
            .Where(d => d.Network_Id == _currentNetwork.Id)
            .ToList();

        var matchingCameras = networkCameras.Where(c => string.Equals(c.Name, target, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingDoorbells = networkDoorbells.Where(d => string.Equals(d.Name, target, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchCount = matchingCameras.Count + matchingDoorbells.Count;

        if (matchCount != 1)
            return $"No device named '{target}' was found. Run LS to see exact names.";

        _currentCamera = matchingCameras.Count == 1 ? matchingCameras[0] : null;
        _currentDoorbell = matchingDoorbells.Count == 1 ? matchingDoorbells[0] : null;
        _currentDeviceName = _currentCamera?.Name ?? _currentDoorbell!.Name;

        return $"Now in device '{_currentDeviceName}'.";
    }

    public string List(TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (library.CurrentSession is null)
            return "Not currently logged into Blink service.";

        if (_currentDeviceName is not null)
        {
            _lastDeviceMediaListing = library.ListDeviceMedia(_currentDeviceName!, _currentCamera, _currentDoorbell, pollInterval, maxRetries);
            _lastDeviceMediaListingFor = _currentDeviceName;
            return _lastDeviceMediaListing.ToListingString();
        }

        if (library.CurrentSession.Networks is null)
            return "Run MAP or GET first to load your devices.";

        if (_currentNetwork is not null)
        {
            var cameras = (library.CurrentSession.Cameras ?? new List<IHomeScreenCamera>())
                .Where(c => c.Network_Id == _currentNetwork.Id);
            var doorbells = (library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>())
                .Where(d => d.Network_Id == _currentNetwork.Id);

            var lines = cameras.Select(DeviceMapExtension.FormatCameraLine)
                .Concat(doorbells.Select(DeviceMapExtension.FormatDoorbellLine))
                .ToList();

            return lines.Count == 0
                ? "No cameras or doorbells in this hub."
                : string.Join(Environment.NewLine, lines);
        }

        return string.Join(Environment.NewLine, library.CurrentSession.Networks.Select(n => n.FormatNetworkLine()));
    }

    public string Trigger(TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (_currentDeviceName is null)
            return "Not in a device directory. cd into a camera or doorbell first.";

        return library.Snap(new[] { "n" + _currentDeviceName }, pollInterval, maxRetries);
    }

    public string DownloadByName(string name, TimeSpan? pollInterval = null, int maxRetries = 120, string? mediaDirectory = null)
    {
        if (_currentDeviceName is null)
            return "Not in a device directory. cd into a camera or doorbell first.";

        if (_lastDeviceMediaListing is null || _lastDeviceMediaListingFor != _currentDeviceName)
            return "Run LS first.";

        var entry = _lastDeviceMediaListing.Entries
            .FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return $"'{name}' wasn't in the last LS output.";

        return entry.Kind switch
        {
            DeviceMediaKind.Current => library.DownloadCurrentThumbnail(_currentDeviceName!, _currentCamera, _currentDoorbell, mediaDirectory),
            DeviceMediaKind.Cloud => library.DownloadCloudItem(entry.CloudItem!, mediaDirectory),
            DeviceMediaKind.Local => library.DownloadLocalClip(entry.SyncModule!, entry.ManifestId!.Value, entry.LocalClip!, pollInterval, maxRetries, mediaDirectory),
            _ => $"'{name}' wasn't in the last LS output.",
        };
    }

    private void ClearAllState()
    {
        _currentNetwork = null;
        ClearDeviceState();
    }

    private void ClearDeviceState()
    {
        _currentCamera = null;
        _currentDoorbell = null;
        _currentDeviceName = null;
        _lastDeviceMediaListing = null;
        _lastDeviceMediaListingFor = null;
    }
}
