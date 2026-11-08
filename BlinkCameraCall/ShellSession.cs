using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall;

public class ShellSession(BlinkLibrary library)
{
    private IHomeScreenNetwork? _currentNetwork;
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
        {
            ClearAllState();
            return "Not currently logged into Blink service.";
        }

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

        var (camera, doorbell) = FindDeviceInCurrentNetwork(target);
        var matchCount = (camera is not null ? 1 : 0) + (doorbell is not null ? 1 : 0);

        if (matchCount != 1)
            return $"No device named '{target}' was found. Run LS to see exact names.";

        _currentDeviceName = camera?.Name ?? doorbell!.Name;

        return $"Now in device '{_currentDeviceName}'.";
    }

    /// <summary>
    /// Resolves a device by name within <see cref="_currentNetwork"/>, the same lookup
    /// <see cref="ChangeDirectory"/> uses to validate a `cd` target. Used by anything that
    /// needs the current device fresh (rather than a cached reference that can go stale
    /// after a Trigger/Snap re-fetches the home screen).
    /// </summary>
    private (IHomeScreenCamera? Camera, IHomeScreenDoorbells? Doorbell) FindDeviceInCurrentNetwork(string name)
    {
        if (_currentNetwork is null || library.CurrentSession is null)
            return (null, null);

        var networkCameras = (library.CurrentSession.Cameras ?? new List<IHomeScreenCamera>())
            .Where(c => c.Network_Id == _currentNetwork.Id)
            .ToList();
        var networkDoorbells = (library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>())
            .Where(d => d.Network_Id == _currentNetwork.Id)
            .ToList();

        var matchingCameras = networkCameras.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingDoorbells = networkDoorbells.Where(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

        var camera = matchingCameras.Count == 1 ? matchingCameras[0] : null;
        var doorbell = matchingDoorbells.Count == 1 ? matchingDoorbells[0] : null;

        return (camera, doorbell);
    }

    /// <summary>
    /// Resolves the camera/doorbell for <see cref="_currentDeviceName"/> fresh from
    /// <see cref="library"/>.CurrentSession, so callers always see the latest Thumbnail/state
    /// (e.g. after a Trigger) instead of whatever was current at `cd` time.
    /// </summary>
    private (IHomeScreenCamera? Camera, IHomeScreenDoorbells? Doorbell) ResolveCurrentDevice() =>
        _currentDeviceName is null ? (null, null) : FindDeviceInCurrentNetwork(_currentDeviceName);

    public string List(TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (library.CurrentSession is null)
        {
            ClearAllState();
            return "Not currently logged into Blink service.";
        }

        if (_currentDeviceName is not null)
        {
            var (camera, doorbell) = ResolveCurrentDevice();
            _lastDeviceMediaListing = library.ListDeviceMedia(_currentDeviceName!, camera, doorbell, pollInterval, maxRetries);
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
        if (library.CurrentSession is null)
        {
            ClearAllState();
            return "Not currently logged into Blink service.";
        }

        if (_currentDeviceName is null)
            return "Not in a device directory. cd into a camera or doorbell first.";

        if (_lastDeviceMediaListing is null || _lastDeviceMediaListingFor != _currentDeviceName)
            return "Run LS first.";

        var entry = _lastDeviceMediaListing.Entries
            .FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return $"'{name}' wasn't in the last LS output.";

        if (entry.Kind == DeviceMediaKind.Current)
        {
            var (camera, doorbell) = ResolveCurrentDevice();
            return library.DownloadCurrentThumbnail(_currentDeviceName!, camera, doorbell, mediaDirectory);
        }

        return entry.Kind switch
        {
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
        _currentDeviceName = null;
        _lastDeviceMediaListing = null;
        _lastDeviceMediaListingFor = null;
    }
}
