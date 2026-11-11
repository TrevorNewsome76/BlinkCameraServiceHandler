using BlinkCameraCall.Driver;
using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.Auth;
using BlinkCommon.Interfaces.System;
using Shadow.Quack;

namespace BlinkCameraCall;

public class BlinkLibrary(IAdapter adapter)
{
    private ISessionDetails _sessionDetails = Duck.Implement<ISessionDetails>(new()).Initialize();
    private List<ICloudMediaItem>? _lastCloudMedia;
    private (long SyncModuleId, long ManifestId, List<ILocalStorageClip> Clips)? _lastLocalManifest;
    private CancellationTokenSource? _watchCts;
    private CancellationTokenSource? _localWatchCts;

    public ISessionDetails? CurrentSession => _sessionDetails.LoggedInStatus ? _sessionDetails : null;

    public bool IsLoggedIn => _sessionDetails.LoggedInStatus;

    public bool TwoFactorPending => !_sessionDetails.LoggedInStatus && _sessionDetails.Verification?.Phone?.Required == true;

    public string Login()
    {
        var loginResponse = adapter.Login();
        _sessionDetails = loginResponse.ConvertToSessionDetails();

        if (_sessionDetails.LoggedInStatus)
        {
            adapter.SetAccessToken(_sessionDetails.Auth?.Token ?? string.Empty);
            _sessionDetails.LoggedInStatus = true;
            return "Login to the Blink Service successful.";
        }
        else
        {
            _sessionDetails.LoggedInStatus = false;
            return $"Login to the Blink Service failed: {loginResponse.Message}";
        }
    }

    public string Login(string[] arguments)
    {
        var loginParameters = arguments.ExtractUsernameAndPassword();
        var loginResponse = adapter.Login(loginParameters[0], loginParameters[1]);
        _sessionDetails = loginResponse.ConvertToSessionDetails();

        if (_sessionDetails.LoggedInStatus)
        {
            adapter.SetAccessToken(_sessionDetails.Auth?.Token ?? string.Empty);
            _sessionDetails.LoggedInStatus = true;
            return "Login to the Blink Service successful.";
        }
        else
        {
            _sessionDetails.LoggedInStatus = false;
            return $"Login to the Blink Service failed: {loginResponse.Message}";
        }
    }

    public string Logout()
    {
        if (_sessionDetails.LoggedInStatus)
        {
            StopWatch();
            StopLocalWatch();
            _sessionDetails.LoggedInStatus = false;
            ILogoutResponse logoutResult =
                adapter.Logout(_sessionDetails.Account ?? Duck.Implement<IAuthAccount>(new()));

            return logoutResult?.Message != null ? "Successfully logged out." : "FAILED to logout.";
        }
        else
        {
            return "Not currently logged into Blink service.";
        }
    }

    public string VerifyPin(string[] arguments)
    {
        var pinCode = arguments.ExtractPinCode();

        if (!_sessionDetails.LoggedInStatus && _sessionDetails.Verification?.Phone?.Required == true)
        {
            var loginResponse = adapter.CompleteTwoFactorLogin(pinCode);
            _sessionDetails = loginResponse.ConvertToSessionDetails();

            if (_sessionDetails.LoggedInStatus)
            {
                adapter.SetAccessToken(_sessionDetails.Auth?.Token ?? string.Empty);
                return "Login to the Blink Service successful.";
            }

            return $"Login to the Blink Service failed: {loginResponse.Message}";
        }

        if (_sessionDetails?.Account is not null && _sessionDetails.LoggedInStatus)
        {
            var setPinResponse = adapter.VerifyPin(_sessionDetails.Account, pinCode);
            return setPinResponse.Message ?? string.Empty;
        }
        else
            return "Not currently logged into Blink service.";
    }

    public string Map()
    {
        if (_sessionDetails?.Account is null || !_sessionDetails.LoggedInStatus)
            return "Not currently logged into Blink service.";

        var response = adapter.GetHomeScreen(_sessionDetails.Account);
        _sessionDetails.Networks = response?.Networks;
        _sessionDetails.SyncModules = response?.Sync_Modules;
        _sessionDetails.Cameras = response?.Cameras;
        _sessionDetails.Doorbells = response?.Doorbells;

        return response is null ? "No devices found." : response.ToDeviceMapString();
    }

    private (IHomeScreenCamera? Camera, IHomeScreenDoorbells? Doorbell) ResolveCameraOrDoorbell(string name, long? networkId = null)
    {
        if (networkId is null)
        {
            var camera = _sessionDetails.FindCameraByName(name);
            var doorbell = _sessionDetails.FindDoorbellByName(name);

            return camera is not null && doorbell is not null ? (null, null) : (camera, doorbell);
        }

        var networkCameras = (_sessionDetails.Cameras ?? new List<IHomeScreenCamera>())
            .Where(c => c.Network_Id == networkId.Value)
            .ToList();
        var networkDoorbells = (_sessionDetails.Doorbells ?? new List<IHomeScreenDoorbells>())
            .Where(d => d.Network_Id == networkId.Value)
            .ToList();

        var matchingCameras = networkCameras.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingDoorbells = networkDoorbells.Where(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

        var scopedCamera = matchingCameras.Count == 1 ? matchingCameras[0] : null;
        var scopedDoorbell = matchingDoorbells.Count == 1 ? matchingDoorbells[0] : null;

        return scopedCamera is not null && scopedDoorbell is not null ? (null, null) : (scopedCamera, scopedDoorbell);
    }

    public string Snap(string[] arguments, TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_sessionDetails.Cameras is null && _sessionDetails.Doorbells is null)
            return "Run MAP or GET first to load your devices.";

        var name = arguments.ExtractDeviceName();
        var (camera, doorbell) = ResolveCameraOrDoorbell(name);

        if (camera is null && doorbell is null)
            return $"No single device named '{name}' was found. Run MAP to see exact names.";

        var result = camera is not null
            ? adapter.TriggerCameraSnapshot(_sessionDetails.Account!, camera.Network_Id, camera.id, pollInterval, maxRetries)
            : adapter.TriggerDoorbellSnapshot(_sessionDetails.Account!, doorbell!.Network_Id, doorbell.Id, pollInterval, maxRetries);

        if (result is null)
            return $"Blink did not finish capturing a new image for '{name}' in time.";

        var response = adapter.GetHomeScreen(_sessionDetails.Account!);
        _sessionDetails.Networks = response?.Networks;
        _sessionDetails.SyncModules = response?.Sync_Modules;
        _sessionDetails.Cameras = response?.Cameras;
        _sessionDetails.Doorbells = response?.Doorbells;

        return $"New image captured for '{name}'. Run IMAGE -n{name} to download it.";
    }

    private void RefreshDeviceLists()
    {
        var response = adapter.GetHomeScreen(_sessionDetails.Account!);
        _sessionDetails.Networks = response?.Networks;
        _sessionDetails.SyncModules = response?.Sync_Modules;
        _sessionDetails.Cameras = response?.Cameras;
        _sessionDetails.Doorbells = response?.Doorbells;
    }

    public string Arm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120, long? networkId = null, CancellationToken cancellationToken = default) =>
        ArmOrDisarm(deviceName, arm: true, pollInterval, maxRetries, networkId, cancellationToken);

    public string Disarm(string deviceName, TimeSpan? pollInterval = null, int maxRetries = 120, long? networkId = null, CancellationToken cancellationToken = default) =>
        ArmOrDisarm(deviceName, arm: false, pollInterval, maxRetries, networkId, cancellationToken);

    private string ArmOrDisarm(string deviceName, bool arm, TimeSpan? pollInterval, int maxRetries, long? networkId = null, CancellationToken cancellationToken = default)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_sessionDetails.Cameras is null && _sessionDetails.Doorbells is null)
            return "Run MAP or GET first to load your devices.";

        var (camera, doorbell) = ResolveCameraOrDoorbell(deviceName, networkId);

        if (camera is null && doorbell is null)
            return $"No single device named '{deviceName}' was found. Run MAP to see exact names.";

        ICommandStatusResponse? result;
        try
        {
            result = camera is not null
                ? (arm
                    ? adapter.EnableCamera(_sessionDetails.Account!, camera.Network_Id, camera.id, pollInterval, maxRetries, cancellationToken)
                    : adapter.DisableCamera(_sessionDetails.Account!, camera.Network_Id, camera.id, pollInterval, maxRetries, cancellationToken))
                : (arm
                    ? adapter.EnableDoorbell(_sessionDetails.Account!, doorbell!.Network_Id, doorbell.Id, pollInterval, maxRetries, cancellationToken)
                    : adapter.DisableDoorbell(_sessionDetails.Account!, doorbell!.Network_Id, doorbell.Id, pollInterval, maxRetries, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return $"{(arm ? "Arming" : "Disarming")} '{deviceName}' cancelled.";
        }

        if (result is null)
            return $"Blink did not finish {(arm ? "arming" : "disarming")} '{deviceName}' in time.";

        var resultText = $"{(arm ? "Armed" : "Disarmed")} '{deviceName}'.";

        try
        {
            RefreshDeviceLists();
        }
        catch (Exception ex)
        {
            resultText += Environment.NewLine + $"Could not refresh device state: {ex.Message}. Run MAP.";
        }

        return resultText;
    }

    public string Arm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        var (lines, succeeded, total, cancelled) = ArmOrDisarmNetwork(network, arm: true, pollInterval, maxRetries, onProgress, cancellationToken);
        var closingLine = cancelled
            ? $"Arming hub '{network.Name}' cancelled ({succeeded}/{total} attempted)."
            : $"Armed hub '{network.Name}' ({succeeded}/{total} succeeded).";
        lines.Add(closingLine);
        onProgress?.Invoke(closingLine);

        try
        {
            RefreshDeviceLists();
        }
        catch (Exception ex)
        {
            var refreshFailureLine = $"Could not refresh device state: {ex.Message}. Run MAP.";
            lines.Add(refreshFailureLine);
            onProgress?.Invoke(refreshFailureLine);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string Disarm(IHomeScreenNetwork network, TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        var (lines, succeeded, total, cancelled) = ArmOrDisarmNetwork(network, arm: false, pollInterval, maxRetries, onProgress, cancellationToken);
        var closingLine = cancelled
            ? $"Disarming hub '{network.Name}' cancelled ({succeeded}/{total} attempted)."
            : $"Disarmed hub '{network.Name}' ({succeeded}/{total} succeeded).";
        lines.Add(closingLine);
        onProgress?.Invoke(closingLine);

        try
        {
            RefreshDeviceLists();
        }
        catch (Exception ex)
        {
            var refreshFailureLine = $"Could not refresh device state: {ex.Message}. Run MAP.";
            lines.Add(refreshFailureLine);
            onProgress?.Invoke(refreshFailureLine);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string Arm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default) =>
        ArmOrDisarmSystem(arm: true, pollInterval, maxRetries, onProgress, cancellationToken);

    public string Disarm(TimeSpan? pollInterval = null, int maxRetries = 120, Action<string>? onProgress = null, CancellationToken cancellationToken = default) =>
        ArmOrDisarmSystem(arm: false, pollInterval, maxRetries, onProgress, cancellationToken);

    private string ArmOrDisarmSystem(bool arm, TimeSpan? pollInterval, int maxRetries, Action<string>? onProgress, CancellationToken cancellationToken)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_sessionDetails.Networks is null)
            return "Run MAP or GET first to load your devices.";

        var done = arm ? "Armed" : "Disarmed";

        if (_sessionDetails.Networks.Count == 0)
            return arm ? "No hubs to arm." : "No hubs to disarm.";

        var lines = new List<string>();
        var grandSucceeded = 0;
        var grandTotal = 0;
        var networksReached = 0;
        var systemCancelled = false;

        foreach (var network in _sessionDetails.Networks)
        {
            var (networkLines, succeeded, total, networkCancelled) = ArmOrDisarmNetwork(network, arm, pollInterval, maxRetries, onProgress, cancellationToken);
            lines.AddRange(networkLines);
            networksReached++;
            grandSucceeded += succeeded;
            grandTotal += total;

            var hubClosingLine = networkCancelled
                ? $"{(arm ? "Arming" : "Disarming")} hub '{network.Name}' cancelled ({succeeded}/{total} attempted)."
                : $"{done} hub '{network.Name}' ({succeeded}/{total} succeeded).";
            lines.Add(hubClosingLine);
            onProgress?.Invoke(hubClosingLine);

            if (networkCancelled)
            {
                systemCancelled = true;
                break;
            }
        }

        var finalLine = systemCancelled
            ? $"{(arm ? "Arming" : "Disarming")} the system cancelled ({grandSucceeded}/{grandTotal} attempted across {networksReached} of {_sessionDetails.Networks.Count} hubs)."
            : $"{done} the system ({grandSucceeded}/{grandTotal} succeeded across {_sessionDetails.Networks.Count} hubs).";
        lines.Add(finalLine);
        onProgress?.Invoke(finalLine);

        try
        {
            RefreshDeviceLists();
        }
        catch (Exception ex)
        {
            var refreshFailureLine = $"Could not refresh device state: {ex.Message}. Run MAP.";
            lines.Add(refreshFailureLine);
            onProgress?.Invoke(refreshFailureLine);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private (List<string> Lines, int Succeeded, int Total, bool Cancelled) ArmOrDisarmNetwork(IHomeScreenNetwork network, bool arm, TimeSpan? pollInterval, int maxRetries, Action<string>? onProgress, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        void Emit(string line) { lines.Add(line); onProgress?.Invoke(line); }

        var succeeded = 0;
        var total = 0;
        var account = _sessionDetails.Account!;
        var verb = arm ? "arming" : "disarming";
        var done = arm ? "armed" : "disarmed";

        if (cancellationToken.IsCancellationRequested)
            return (lines, succeeded, total, true);

        total++;
        try
        {
            var networkResult = arm
                ? adapter.ArmNetwork(account, network.Id, pollInterval, maxRetries, cancellationToken)
                : adapter.DisarmNetwork(account, network.Id, pollInterval, maxRetries, cancellationToken);

            if (networkResult is not null)
            {
                Emit($"Hub '{network.Name}': {done}.");
                succeeded++;
            }
            else
            {
                Emit($"Hub '{network.Name}': Blink did not finish {verb} '{network.Name}' in time.");
            }
        }
        catch (OperationCanceledException)
        {
            return (lines, succeeded, total, true);
        }
        catch (Exception ex)
        {
            Emit($"Hub '{network.Name}': {ex.Message}");
        }

        foreach (var camera in (_sessionDetails.Cameras ?? new List<IHomeScreenCamera>()).Where(c => c.Network_Id == network.Id))
        {
            if (cancellationToken.IsCancellationRequested)
                return (lines, succeeded, total, true);

            total++;
            try
            {
                var result = arm
                    ? adapter.EnableCamera(account, camera.Network_Id, camera.id, pollInterval, maxRetries, cancellationToken)
                    : adapter.DisableCamera(account, camera.Network_Id, camera.id, pollInterval, maxRetries, cancellationToken);

                if (result is not null)
                {
                    Emit($"  Camera '{camera.Name}': {done}.");
                    succeeded++;
                }
                else
                {
                    Emit($"  Camera '{camera.Name}': Blink did not finish {verb} '{camera.Name}' in time.");
                }
            }
            catch (OperationCanceledException)
            {
                return (lines, succeeded, total, true);
            }
            catch (Exception ex)
            {
                Emit($"  Camera '{camera.Name}': {ex.Message}");
            }
        }

        foreach (var doorbell in (_sessionDetails.Doorbells ?? new List<IHomeScreenDoorbells>()).Where(d => d.Network_Id == network.Id))
        {
            if (cancellationToken.IsCancellationRequested)
                return (lines, succeeded, total, true);

            total++;
            try
            {
                var result = arm
                    ? adapter.EnableDoorbell(account, doorbell.Network_Id, doorbell.Id, pollInterval, maxRetries, cancellationToken)
                    : adapter.DisableDoorbell(account, doorbell.Network_Id, doorbell.Id, pollInterval, maxRetries, cancellationToken);

                if (result is not null)
                {
                    Emit($"  Doorbell '{doorbell.Name}': {done}.");
                    succeeded++;
                }
                else
                {
                    Emit($"  Doorbell '{doorbell.Name}': Blink did not finish {verb} '{doorbell.Name}' in time.");
                }
            }
            catch (OperationCanceledException)
            {
                return (lines, succeeded, total, true);
            }
            catch (Exception ex)
            {
                Emit($"  Doorbell '{doorbell.Name}': {ex.Message}");
            }
        }

        return (lines, succeeded, total, false);
    }

    public string Device(string[] arguments)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_sessionDetails.Cameras is null && _sessionDetails.Doorbells is null)
            return "Run MAP or GET first to load your devices.";

        var name = arguments.ExtractDeviceName();
        var camera = _sessionDetails.FindCameraByName(name);
        var doorbell = camera is null ? _sessionDetails.FindDoorbellByName(name) : null;

        if (camera is null && doorbell is null)
            return $"No single device named '{name}' was found. Run MAP to see exact names.";

        var networkId = camera?.Network_Id ?? doorbell!.Network_Id;
        var network = _sessionDetails.Networks?.FirstOrDefault(n => n.Id == networkId)
            ?? Duck.Implement<IHomeScreenNetwork>(new { Name = "Unknown", Armed = false });

        return camera is not null
            ? camera.ToDeviceDetailString(network)
            : doorbell!.ToDeviceDetailString(network);
    }

    public string Image(string[] arguments, string? mediaDirectory = null)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_sessionDetails.Cameras is null && _sessionDetails.Doorbells is null)
            return "Run MAP or GET first to load your devices.";

        var name = arguments.ExtractDeviceName();
        var (camera, doorbell) = ResolveCameraOrDoorbell(name);

        if (camera is null && doorbell is null)
            return $"No single device named '{name}' was found. Run MAP to see exact names.";

        return DownloadCurrentThumbnail(name, camera, doorbell, mediaDirectory);
    }

    internal string DownloadCurrentThumbnail(string name, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell, string? mediaDirectory = null)
    {
        var thumbnailField = camera?.Thumbnail ?? doorbell!.Thumbnail;
        if (string.IsNullOrEmpty(thumbnailField))
            return $"'{name}' has no image yet — run SNAP -n{name} first.";

        var account = _sessionDetails.Account!;
        var networkId = camera?.Network_Id ?? doorbell!.Network_Id;
        var deviceId = camera?.id ?? doorbell!.Id;
        var url = ThumbnailUrlExtension.BuildThumbnailUrl(thumbnailField, account.Tier, account.Account_Id, networkId, deviceId, isDoorbell: doorbell is not null);

        var bytes = adapter.DownloadBytes(url);
        if (bytes is null)
            return $"Blink did not return an image for '{name}'.";

        var path = MediaFileExtension.SaveMediaFile(name, bytes, "jpg", mediaDirectory);
        return $"Saved image to {path}.";
    }

    public string Videos(string[] arguments)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        var hours = arguments.ExtractHours();
        var response = adapter.GetRecentMedia(_sessionDetails.Account!, DateTime.UtcNow.AddHours(-hours));
        _lastCloudMedia = (response?.Media ?? new List<ICloudMediaItem>()).Where(m => !m.Deleted).ToList();

        return _lastCloudMedia.ToCloudMediaListString(hours);
    }

    public string Watch(string[] arguments, Action<string> onAlert, int persistedIntervalSeconds = 20, Action<int>? onIntervalPersisted = null)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_watchCts is not null)
            return "Already watching. Use UNWATCH to stop.";

        var explicitIntervalSeconds = ExtractExplicitWatchIntervalSeconds(arguments);
        int startingSeconds;
        if (explicitIntervalSeconds.HasValue)
        {
            startingSeconds = explicitIntervalSeconds.Value;
            onIntervalPersisted?.Invoke(startingSeconds);
        }
        else
        {
            startingSeconds = persistedIntervalSeconds;
        }

        var account = _sessionDetails.Account!;
        var cts = new CancellationTokenSource();
        _watchCts = cts;
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(startingSeconds));

        Task.Run(() => EventWatchPolling.Run(
            adapter,
            account,
            item => onAlert($"[ALERT] New event: {item.Device_Name} at {item.Created_At:yyyy-MM-dd HH:mm:ss} UTC (id {item.Id})"),
            ex => onAlert($"[WATCH] Error checking for new events: {ex.Message}"),
            cts.Token,
            interval,
            (total, slowest, newInterval) => onAlert($"[WATCH] Checking for new events took {total.TotalSeconds:0.0}s — longer than the current interval, extending it to {newInterval.TotalSeconds:0.0}s."),
            newInterval => onIntervalPersisted?.Invoke((int)Math.Ceiling(newInterval.TotalSeconds))));

        return $"Watching for new events every {startingSeconds}s. Use UNWATCH to stop.";
    }

    private static int? ExtractExplicitWatchIntervalSeconds(string[] arguments)
    {
        foreach (var argument in arguments)
            if (argument.StartsWith('s') && argument.Length > 1 && int.TryParse(argument.Substring(1), out var seconds))
                return seconds;

        return null;
    }

    public string Unwatch()
    {
        if (_watchCts is null)
            return "Not currently watching.";

        StopWatch();
        return "Stopped watching.";
    }

    private void StopWatch()
    {
        if (_watchCts is null)
            return;

        _watchCts.Cancel();
        _watchCts = null;
    }

    public string LocalWatch(string[] arguments, Action<string> onAlert, int persistedIntervalSeconds = 5, Action<int>? onIntervalPersisted = null)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_localWatchCts is not null)
            return "Already watching local storage. Use UNLOCALWATCH to stop.";

        if (_sessionDetails.SyncModules is null)
            return "Run MAP or GET first to load your devices.";

        var syncModules = _sessionDetails.SyncModules
            .Where(s => s.Local_Storage_Status == "active")
            .ToList();

        if (syncModules.Count == 0)
            return "No sync modules with active local storage to watch.";

        var explicitIntervalSeconds = ExtractExplicitWatchIntervalSeconds(arguments);
        int startingSeconds;
        if (explicitIntervalSeconds.HasValue)
        {
            startingSeconds = explicitIntervalSeconds.Value;
            onIntervalPersisted?.Invoke(startingSeconds);
        }
        else
        {
            startingSeconds = persistedIntervalSeconds;
        }

        var account = _sessionDetails.Account!;
        var cts = new CancellationTokenSource();
        _localWatchCts = cts;
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(startingSeconds));

        Task.Run(() => LocalStorageWatchPolling.Run(
            adapter,
            account,
            syncModules,
            (clip, syncModuleName) => onAlert($"[LOCAL ALERT] New clip: {clip.Camera_Name} on '{syncModuleName}' at {clip.Created_At:yyyy-MM-dd HH:mm:ss} UTC (id {clip.Id})"),
            (ex, syncModuleName) => onAlert($"[LOCALWATCH] Error checking '{syncModuleName}': {ex.Message}"),
            (total, slowest, newInterval) => onAlert($"[LOCALWATCH] Checking local storage took {total.TotalSeconds:0.0}s across {syncModules.Count} sync module(s) (slowest: {slowest.TotalSeconds:0.0}s) — longer than the current interval, extending it to {newInterval.TotalSeconds:0.0}s."),
            newInterval => onIntervalPersisted?.Invoke((int)Math.Ceiling(newInterval.TotalSeconds)),
            interval,
            cts.Token));

        return $"Watching local storage for new clips every {startingSeconds}s across {syncModules.Count} sync module(s). Use UNLOCALWATCH to stop.";
    }

    public string Unlocalwatch()
    {
        if (_localWatchCts is null)
            return "Not currently watching local storage.";

        StopLocalWatch();
        return "Stopped watching local storage.";
    }

    private void StopLocalWatch()
    {
        if (_localWatchCts is null)
            return;

        _localWatchCts.Cancel();
        _localWatchCts = null;
    }

    public string Download(string[] arguments, string? mediaDirectory = null)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        var id = arguments.ExtractMediaId();
        var item = _lastCloudMedia?.FirstOrDefault(m => m.Id == id);

        if (item is null)
            return $"Run VIDEOS first, or '{id}' wasn't in the last VIDEOS list.";

        return DownloadCloudItem(item, mediaDirectory);
    }

    internal string DownloadCloudItem(ICloudMediaItem item, string? mediaDirectory = null)
    {
        var url = $"https://rest-{_sessionDetails.Account!.Tier}.immedia-semi.com{item.Media}";
        var bytes = adapter.DownloadBytes(url);
        if (bytes is null)
            return $"Blink did not return a clip for '{item.Id}'.";

        var path = MediaFileExtension.SaveMediaFile(item.Device_Name, bytes, "mp4", mediaDirectory);
        return $"Saved video to {path}.";
    }

    public string LocalClips(string[] arguments, TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        if (_sessionDetails.SyncModules is null)
            return "Run MAP or GET first to load your devices.";

        var name = arguments.ExtractDeviceName();
        var syncModule = _sessionDetails.FindSyncModuleByName(name);

        if (syncModule is null)
            return $"No single device named '{name}' was found. Run MAP to see exact names.";

        if (syncModule.Local_Storage_Status != "active")
            return $"'{name}' doesn't have active local storage.";

        var account = _sessionDetails.Account!;
        var networkId = long.Parse(syncModule.Network_Id);

        var requestResult = adapter.RequestLocalStorageManifest(account, networkId, syncModule.Id);
        var manifestResult = adapter.GetLocalStorageManifest(account, networkId, syncModule.Id, requestResult!.Id, pollInterval, maxRetries);

        if (manifestResult is null)
            return $"Blink did not finish reading the SD-card manifest for '{name}' in time.";

        var clips = manifestResult.Clips ?? new List<ILocalStorageClip>();
        _lastLocalManifest = (syncModule.Id, manifestResult.Manifest_Id, clips);

        return clips.ToLocalClipsListString(name);
    }

    public string LocalDownload(string[] arguments, TimeSpan? pollInterval = null, int maxRetries = 120, string? mediaDirectory = null)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        var name = arguments.ExtractDeviceName();
        var syncModule = _sessionDetails.FindSyncModuleByName(name);

        if (syncModule is null)
            return $"No single device named '{name}' was found. Run MAP to see exact names.";

        if (_lastLocalManifest is null || _lastLocalManifest.Value.SyncModuleId != syncModule.Id)
            return $"Run LOCALCLIPS -n{name} first.";

        var id = arguments.ExtractMediaId();
        var clip = _lastLocalManifest.Value.Clips.FirstOrDefault(c => c.Id == id);

        if (clip is null)
            return $"'{id}' wasn't in the last LOCALCLIPS list for '{name}'.";

        var manifestId = _lastLocalManifest.Value.ManifestId;

        return DownloadLocalClip(syncModule, manifestId, clip, pollInterval, maxRetries, mediaDirectory);
    }

    internal string DownloadLocalClip(IHomeScreenSyncModule syncModule, long manifestId, ILocalStorageClip clip, TimeSpan? pollInterval = null, int maxRetries = 120, string? mediaDirectory = null)
    {
        var account = _sessionDetails.Account!;
        var networkId = long.Parse(syncModule.Network_Id);

        var prepareResult = adapter.PrepareLocalStorageClip(account, networkId, syncModule.Id, manifestId, clip.Id, pollInterval, maxRetries);
        if (prepareResult is null)
            return $"Blink did not finish preparing the clip for '{syncModule.Name}' in time.";

        var url = adapter.BuildLocalStorageClipUrl(account, networkId, syncModule.Id, manifestId, clip.Id);
        var bytes = adapter.DownloadBytes(url);
        if (bytes is null)
            return $"Blink did not return the clip for '{syncModule.Name}'.";

        var path = MediaFileExtension.SaveMediaFile(clip.Camera_Name, bytes, "mp4", mediaDirectory);
        return $"Saved video to {path}.";
    }

    internal DeviceMediaListing ListDeviceMedia(string deviceName, IHomeScreenCamera? camera, IHomeScreenDoorbells? doorbell, TimeSpan? pollInterval = null, int maxRetries = 120)
    {
        var entries = new List<DeviceMediaEntry>();

        var thumbnailField = camera?.Thumbnail ?? doorbell?.Thumbnail;
        if (!string.IsNullOrEmpty(thumbnailField))
            entries.Add(new DeviceMediaEntry("current", DeviceMediaKind.Current, null, null, null, null, null));

        var cloudMedia = adapter.GetRecentMedia(_sessionDetails.Account!, DateTime.UtcNow.AddHours(-24))?.Media ?? new List<ICloudMediaItem>();
        foreach (var item in cloudMedia.Where(m => !m.Deleted && string.Equals(m.Device_Name, deviceName, StringComparison.OrdinalIgnoreCase)))
            entries.Add(new DeviceMediaEntry(item.Created_At.ToUniversalTime().ToString("yyyy-MM-dd_HH-mm-ss"), DeviceMediaKind.Cloud, item.Created_At, item, null, null, null));

        var networkId = camera?.Network_Id ?? doorbell!.Network_Id;
        var syncModule = _sessionDetails.SyncModules?.FirstOrDefault(s => s.Network_Id == networkId.ToString());

        if (syncModule is not null && syncModule.Local_Storage_Status == "active")
        {
            var account = _sessionDetails.Account!;
            var syncNetworkId = long.Parse(syncModule.Network_Id);

            var requestResult = adapter.RequestLocalStorageManifest(account, syncNetworkId, syncModule.Id);
            var manifestResult = adapter.GetLocalStorageManifest(account, syncNetworkId, syncModule.Id, requestResult!.Id, pollInterval, maxRetries);

            if (manifestResult is not null)
            {
                var clips = manifestResult.Clips ?? new List<ILocalStorageClip>();
                foreach (var clip in clips.Where(c => string.Equals(c.Camera_Name, deviceName, StringComparison.OrdinalIgnoreCase)))
                    entries.Add(new DeviceMediaEntry(clip.Created_At.ToUniversalTime().ToString("yyyy-MM-dd_HH-mm-ss"), DeviceMediaKind.Local, clip.Created_At, null, clip, syncModule, manifestResult.Manifest_Id));
            }
        }

        var seenNames = new HashSet<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!seenNames.Add(entry.Name))
                entries[i] = entry with { Name = $"{entry.Name}_{entry.CloudItem?.Id ?? entry.LocalClip!.Id}" };
        }

        return new DeviceMediaListing(entries);
    }

    public string Help(string[] arguments)
    {
        return new string("LOGIN            Logs into Blink account (using settings file." +
                          System.Environment.NewLine +
                          "VERIFY <code>       Verifies sms pin number sent after new login." +
                          System.Environment.NewLine +
                          "QUIT, EXIT       Exits program." + System.Environment.NewLine +
                          "HELP             Provides Help information for Windows commands." +
                          System.Environment.NewLine
        );
    }

    public string Get(string[] arguments)
    {
        var getParameter = arguments.ExtractHomeScreenGetCommand();

        if (_sessionDetails?.Account is not null && _sessionDetails.LoggedInStatus)
        {

            IGetHomeScreenResponse response = Duck.Implement<IGetHomeScreenResponse>(new());

            switch (getParameter.ToLower())
            {
                case "home":
                    response = adapter.GetHomeScreen(_sessionDetails.Account);
                    break;
                default: return "Unrecognised HomeSystem command";
            }
            _sessionDetails.Networks = response?.Networks;
            _sessionDetails.SyncModules = response?.Sync_Modules;
            _sessionDetails.Cameras = response?.Cameras;
            _sessionDetails.Doorbells = response?.Doorbells;

            return "Command ran successfully.";
        }
        else
        {
            return $"Not logged in to a system so this command cannot be used.";
        }
    }
}