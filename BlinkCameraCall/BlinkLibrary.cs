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

    private (IHomeScreenCamera? Camera, IHomeScreenDoorbells? Doorbell) ResolveCameraOrDoorbell(string name)
    {
        var camera = _sessionDetails.FindCameraByName(name);
        var doorbell = _sessionDetails.FindDoorbellByName(name);

        return camera is not null && doorbell is not null ? (null, null) : (camera, doorbell);
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

    public string Download(string[] arguments, string? mediaDirectory = null)
    {
        if (!(_sessionDetails?.LoggedInStatus ?? false))
            return "Not currently logged into Blink service.";

        var id = arguments.ExtractMediaId();
        var item = _lastCloudMedia?.FirstOrDefault(m => m.Id == id);

        if (item is null)
            return $"Run VIDEOS first, or '{id}' wasn't in the last VIDEOS list.";

        var url = $"https://rest-{_sessionDetails.Account!.Tier}.immedia-semi.com{item.Media}";
        var bytes = adapter.DownloadBytes(url);
        if (bytes is null)
            return $"Blink did not return a clip for '{id}'.";

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

        var account = _sessionDetails.Account!;
        var networkId = long.Parse(syncModule.Network_Id);
        var manifestId = _lastLocalManifest.Value.ManifestId;

        var prepareResult = adapter.PrepareLocalStorageClip(account, networkId, syncModule.Id, manifestId, id, pollInterval, maxRetries);
        if (prepareResult is null)
            return $"Blink did not finish preparing the clip for '{name}' in time.";

        var url = adapter.BuildLocalStorageClipUrl(account, networkId, syncModule.Id, manifestId, id);
        var bytes = adapter.DownloadBytes(url);
        if (bytes is null)
            return $"Blink did not return the clip for '{name}'.";

        var path = MediaFileExtension.SaveMediaFile(clip.Camera_Name, bytes, "mp4", mediaDirectory);
        return $"Saved video to {path}.";
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