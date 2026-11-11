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
                .Where(c => c.Network_Id == _currentNetwork.Id).ToList();
            var doorbells = (library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>())
                .Where(d => d.Network_Id == _currentNetwork.Id).ToList();

            if (cameras.Count == 0 && doorbells.Count == 0)
                return "No cameras or doorbells in this hub.";

            var media = library.ListAllDeviceMedia(cameras, doorbells);
            return string.Join(Environment.NewLine, FormatDevicesWithMedia(cameras, doorbells, media));
        }

        var allCameras = library.CurrentSession.Cameras ?? new List<IHomeScreenCamera>();
        var allDoorbells = library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>();
        var allMedia = library.ListAllDeviceMedia(allCameras, allDoorbells);

        var blocks = library.CurrentSession.Networks.Select(network =>
        {
            var networkCameras = allCameras.Where(c => c.Network_Id == network.Id).ToList();
            var networkDoorbells = allDoorbells.Where(d => d.Network_Id == network.Id).ToList();

            var lines = new List<string> { network.FormatNetworkLine() };
            lines.AddRange(networkCameras.Count == 0 && networkDoorbells.Count == 0
                ? new[] { "  (no cameras or doorbells)" }
                : FormatDevicesWithMedia(networkCameras, networkDoorbells, allMedia));

            return string.Join(Environment.NewLine, lines);
        });

        return string.Join(Environment.NewLine, blocks);
    }

    // Slower sibling of List(TimeSpan?, int) above: additionally expands the same hub/root-level
    // listing with local storage, one sync module at a time (request-then-poll, same as LOCALCLIPS),
    // streaming a short summary per device through onProgress as each sync module resolves. Single-
    // device LS is unaffected — it already includes local storage and stays on the plain List() path.
    // Cancelling finishes whatever sync module is currently in flight, then stops; a failed sync
    // module is reported inline and checking continues with the next one.
    public string List(Action<string> onProgress, CancellationToken cancellationToken, TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (library.CurrentSession is null)
        {
            ClearAllState();
            return "Not currently logged into Blink service.";
        }

        if (_currentDeviceName is not null)
            return List(pollInterval, maxRetries);

        if (library.CurrentSession.Networks is null)
            return "Run MAP or GET first to load your devices.";

        if (_currentNetwork is not null)
        {
            var cameras = (library.CurrentSession.Cameras ?? new List<IHomeScreenCamera>())
                .Where(c => c.Network_Id == _currentNetwork.Id).ToList();
            var doorbells = (library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>())
                .Where(d => d.Network_Id == _currentNetwork.Id).ToList();

            if (cameras.Count == 0 && doorbells.Count == 0)
                return "No cameras or doorbells in this hub.";

            var media = library.ListAllDeviceMedia(cameras, doorbells);
            var cancelNote = AddLocalStorage(cameras, doorbells, media, onProgress, cancellationToken, pollInterval, maxRetries);

            var lines = FormatDevicesWithMedia(cameras, doorbells, media);
            if (cancelNote is not null)
                lines.Add(cancelNote);

            return string.Join(Environment.NewLine, lines);
        }

        var allCameras = library.CurrentSession.Cameras ?? new List<IHomeScreenCamera>();
        var allDoorbells = library.CurrentSession.Doorbells ?? new List<IHomeScreenDoorbells>();
        var allMedia = library.ListAllDeviceMedia(allCameras, allDoorbells);
        var rootCancelNote = AddLocalStorage(allCameras, allDoorbells, allMedia, onProgress, cancellationToken, pollInterval, maxRetries);

        var allBlocks = library.CurrentSession.Networks.Select(network =>
        {
            var networkCameras = allCameras.Where(c => c.Network_Id == network.Id).ToList();
            var networkDoorbells = allDoorbells.Where(d => d.Network_Id == network.Id).ToList();

            var lines = new List<string> { network.FormatNetworkLine() };
            lines.AddRange(networkCameras.Count == 0 && networkDoorbells.Count == 0
                ? new[] { "  (no cameras or doorbells)" }
                : FormatDevicesWithMedia(networkCameras, networkDoorbells, allMedia));

            return string.Join(Environment.NewLine, lines);
        }).ToList();

        var result = string.Join(Environment.NewLine, allBlocks);
        return rootCancelNote is null ? result : result + Environment.NewLine + rootCancelNote;
    }

    // Returns a trailing "(cancelled ...)" note if cancellation stopped it early, else null.
    private string? AddLocalStorage(
        List<IHomeScreenCamera> cameras,
        List<IHomeScreenDoorbells> doorbells,
        (Dictionary<long, DeviceMediaListing> Cameras, Dictionary<long, DeviceMediaListing> Doorbells) media,
        Action<string> onProgress,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval,
        int maxRetries)
    {
        var syncModules = (library.CurrentSession!.SyncModules ?? new List<IHomeScreenSyncModule>())
            .Where(s => s.Local_Storage_Status == "active")
            .Where(s => cameras.Any(c => c.Network_Id.ToString() == s.Network_Id) || doorbells.Any(d => d.Network_Id.ToString() == s.Network_Id))
            .ToList();

        for (var i = 0; i < syncModules.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return CancelledNote(syncModules.Count - i);

            var syncModule = syncModules[i];
            List<ILocalStorageClip> clips;
            try
            {
                // Deliberately CancellationToken.None, not the live token: this sync module is
                // already "in flight" once started, and the agreed behavior is to finish it
                // rather than interrupt it mid-poll (the live token is only re-checked at the
                // top of the loop, before the *next* sync module starts). LOCALWATCH's own
                // background loop is the caller that actually needs mid-poll interruption.
                var manifest = library.FetchSyncModuleManifest(syncModule, CancellationToken.None, pollInterval, maxRetries);
                clips = manifest?.Clips ?? new List<ILocalStorageClip>();
            }
            catch (Exception ex)
            {
                foreach (var camera in cameras.Where(c => c.Network_Id.ToString() == syncModule.Network_Id))
                {
                    AppendLocalStorageError(media.Cameras, camera.id, ex.Message);
                    onProgress($"{camera.Name}: local storage unavailable: {ex.Message}");
                }
                foreach (var doorbell in doorbells.Where(d => d.Network_Id.ToString() == syncModule.Network_Id))
                {
                    AppendLocalStorageError(media.Doorbells, doorbell.Id, ex.Message);
                    onProgress($"{doorbell.Name}: local storage unavailable: {ex.Message}");
                }
                continue;
            }

            foreach (var camera in cameras.Where(c => c.Network_Id.ToString() == syncModule.Network_Id))
            {
                var added = AppendLocalClips(media.Cameras, camera.id, camera.Name, clips);
                if (added > 0)
                    onProgress($"{camera.Name}: found {added} new local clip(s).");
            }
            foreach (var doorbell in doorbells.Where(d => d.Network_Id.ToString() == syncModule.Network_Id))
            {
                var added = AppendLocalClips(media.Doorbells, doorbell.Id, doorbell.Name, clips);
                if (added > 0)
                    onProgress($"{doorbell.Name}: found {added} new local clip(s).");
            }
        }

        return null;

        static string CancelledNote(int remaining) =>
            $"(cancelled — stopped before checking {remaining} more sync module(s) for local storage.)";
    }

    private static int AppendLocalClips(Dictionary<long, DeviceMediaListing> listings, long deviceId, string deviceName, List<ILocalStorageClip> clips)
    {
        if (!listings.TryGetValue(deviceId, out var listing))
            listings[deviceId] = listing = new DeviceMediaListing(new List<DeviceMediaEntry>());

        var seenNames = listing.Entries.Select(e => e.Name).ToHashSet();
        var added = 0;

        foreach (var clip in clips.Where(c => string.Equals(c.Camera_Name, deviceName, StringComparison.OrdinalIgnoreCase)))
        {
            var name = clip.Created_At.ToUniversalTime().ToString("yyyy-MM-dd_HH-mm-ss");
            if (!seenNames.Add(name))
            {
                name = $"{name}_{clip.Id}";
                seenNames.Add(name);
            }

            listing.Entries.Add(new DeviceMediaEntry(name, DeviceMediaKind.Local, clip.Created_At, null, clip, null, null));
            added++;
        }

        return added;
    }

    private static void AppendLocalStorageError(Dictionary<long, DeviceMediaListing> listings, long deviceId, string reason)
    {
        if (!listings.TryGetValue(deviceId, out var listing))
            listings[deviceId] = listing = new DeviceMediaListing(new List<DeviceMediaEntry>());

        listing.Entries.Add(new DeviceMediaEntry($"(local storage unavailable: {reason})", DeviceMediaKind.Error, null, null, null, null, null));
    }

    private static List<string> FormatDevicesWithMedia(
        List<IHomeScreenCamera> cameras,
        List<IHomeScreenDoorbells> doorbells,
        (Dictionary<long, DeviceMediaListing> Cameras, Dictionary<long, DeviceMediaListing> Doorbells) media)
    {
        var lines = new List<string>();

        foreach (var camera in cameras)
        {
            lines.Add(DeviceMapExtension.FormatCameraLine(camera));
            if (media.Cameras.TryGetValue(camera.id, out var listing))
                lines.AddRange(listing.IndentedLines("    "));
        }

        foreach (var doorbell in doorbells)
        {
            lines.Add(DeviceMapExtension.FormatDoorbellLine(doorbell));
            if (media.Doorbells.TryGetValue(doorbell.Id, out var listing))
                lines.AddRange(listing.IndentedLines("    "));
        }

        return lines;
    }

    public string Trigger(TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (_currentDeviceName is null)
            return "Not in a device directory. cd into a camera or doorbell first.";

        return library.Snap(new[] { "n" + _currentDeviceName }, pollInterval, maxRetries);
    }

    public string Arm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default) =>
        _currentDeviceName is not null ? library.Arm(_currentDeviceName, pollInterval, maxRetries, _currentNetwork!.Id, cancellationToken) :
        _currentNetwork is not null ? library.Arm(_currentNetwork, pollInterval, maxRetries, onProgress, cancellationToken) :
        library.Arm(pollInterval, maxRetries, onProgress, cancellationToken);

    public string Disarm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default) =>
        _currentDeviceName is not null ? library.Disarm(_currentDeviceName, pollInterval, maxRetries, _currentNetwork!.Id, cancellationToken) :
        _currentNetwork is not null ? library.Disarm(_currentNetwork, pollInterval, maxRetries, onProgress, cancellationToken) :
        library.Disarm(pollInterval, maxRetries, onProgress, cancellationToken);

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
