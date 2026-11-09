using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;

namespace BlinkCameraCallUnitTests;

internal class MockHttpClientApiHandler : IApiMethods, IDisposable
{
    public string AccessToken { get; set; } = string.Empty;

    public int HomeScreenCallCount { get; private set; }

    public bool SetAccessToken(string accessToken)
    {
        if (accessToken == "BU8fOjaF4E5POf4WTRm5wA")
        {
            AccessToken = accessToken;
            return true;
        }
        else
        {
            return false;
        }
    }

    public string Post(string url, List<KeyValuePair<string, string>> parameters)
    {
        throw new NotImplementedException();
    }

    public string? Post(string Url, string body)
    {
        return Url switch
        {
            "https://rest-007.immedia-semi.com/api/v4/account/134166/client/1486061/pin/verify" =>
                body == "{\"pin\":\"987654\"}"
                    ? MockData.AuthCorrectPinResponse().Serialize()
                    : MockData.AuthIncorrectPinResponse().Serialize(),
            _ => throw new NotImplementedException()
        };
    }

    public string? Post(string Url)
    {
        switch (Url)
        {
            case "https://rest-prod.immedia-semi.com/api/v4/account/134166/client/1486061/logout":
                return MockData.AuthLogoutCorrectResponse().Serialize();
            case "https://rest-007.immedia-semi.com/network/263178/camera/321343/thumbnail":
                return "{\"id\":500,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/camera/321380/thumbnail":
                return "{\"id\":501,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/camera/321343/enable":
                return "{\"id\":1010,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/camera/321343/disable":
                return "{\"id\":1011,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/camera/321380/enable":
                return "{\"id\":1012,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/camera/321380/disable":
                return "{\"message\":\"system busy\"}";
            case "https://rest-007.immedia-semi.com/network/263178/arm":
                return "{\"id\":1020,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/disarm":
                return "{\"id\":1021,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263310/arm":
                return "{\"id\":1022,\"network_id\":263310,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263310/disarm":
                return "{\"message\":\"system busy\"}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/doorbells/5009/enable":
                return "{\"id\":1014,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/doorbells/5009/disable":
                return "{\"id\":1015,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/1/camera/10/thumbnail":
                return "{\"message\":\"system busy\"}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/500/local_storage/manifest/request":
                return "{\"id\":600}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/502/local_storage/manifest/request":
                return "{\"id\":601}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/503/local_storage/manifest/request":
                return "{\"id\":602}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/504/local_storage/manifest/request":
                return "{\"id\":603}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/505/local_storage/manifest/request":
                return "{\"message\":\"system busy\"}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/506/local_storage/manifest/request":
                return "{\"id\":604}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/506/local_storage/manifest/703/clip/request/4":
                return "{\"message\":\"system busy\"}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/500/local_storage/manifest/700/clip/request/1":
                return "{\"id\":900}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/504/local_storage/manifest/702/clip/request/2":
                return "{\"id\":801}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/request":
                return "{\"id\":610}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/710/clip/request/11":
                return "{\"id\":920}";
            default:
                return MockData.AuthLogoutIncorrectResponse().Serialize();
        }
    }

    public string? Put(string Url, string serializedJsonString)
    {
        throw new NotImplementedException();
    }

    public string? Get(string Url)
    {
        if (Url.StartsWith("https://rest-007.immedia-semi.com/api/v1/accounts/134166/media/changed?since="))
            return MockData.CreateCloudMediaChangedResponse().Serialize();

        if (Url.StartsWith("https://rest-007.immedia-semi.com/api/v1/accounts/999999/media/changed?since="))
            return MockData.CreateEmptyCloudMediaChangedResponse().Serialize();

        switch (Url)
        {
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/500/local_storage/manifest/request/600":
                return "{\"manifest_id\":700,\"clips\":[{\"id\":1,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-01T12:00:00+00:00\",\"size\":12345}]}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/502/local_storage/manifest/request/601":
                return "{\"manifest_id\":701,\"clips\":[]}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/503/local_storage/manifest/request/602":
                return "{\"id\":602}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/504/local_storage/manifest/request/603":
                return "{\"manifest_id\":702,\"clips\":[{\"id\":2,\"camera_name\":\"Garage\",\"created_at\":\"2026-11-01T14:00:00+00:00\",\"size\":9999}]}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/506/local_storage/manifest/request/604":
                return "{\"manifest_id\":703,\"clips\":[{\"id\":4,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-01T15:00:00+00:00\",\"size\":1111}]}";
            case "https://rest-007.immedia-semi.com/network/1/command/900":
                return "{\"id\":900,\"network_id\":1,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/1/command/801":
                return "{\"id\":801,\"network_id\":1,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/request/610":
                return "{\"manifest_id\":710,\"clips\":[" +
                    "{\"id\":11,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-02T08:00:00+00:00\",\"size\":5555}," +
                    "{\"id\":12,\"camera_name\":\"Pergoda\",\"created_at\":\"2026-11-01T12:00:00+00:00\",\"size\":4444}," +
                    "{\"id\":13,\"camera_name\":\"Garage\",\"created_at\":\"2026-11-01T09:00:00+00:00\",\"size\":2222}]}";
            case "https://rest-007.immedia-semi.com/network/263178/command/920":
                return "{\"id\":920,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/api/v3/accounts/134166/homescreen":
                HomeScreenCallCount++;
                return (HomeScreenCallCount > 1
                    ? MockData.CreateValidHomeScreenResponseWithUpdatedThumbnail()
                    : MockData.CreateValidHomeScreenResponse()).Serialize();
            case "https://rest-007.immedia-semi.com/api/v3/accounts/999999/homescreen":
                return MockData.CreateHomeScreenResponseWithEmptyThumbnail().Serialize();
            case "https://rest-007.immedia-semi.com/network/263178/command/500":
                return "{\"id\":500,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/501":
                return "{\"id\":501,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1010":
                return "{\"id\":1010,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1011":
                return "{\"id\":1011,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1012":
                return "{\"id\":1012,\"network_id\":263178,\"complete\":false,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1014":
                return "{\"id\":1014,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1015":
                return "{\"id\":1015,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1020":
                return "{\"id\":1020,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263178/command/1021":
                return "{\"id\":1021,\"network_id\":263178,\"complete\":true,\"status_code\":908}";
            case "https://rest-007.immedia-semi.com/network/263310/command/1022":
                return "{\"id\":1022,\"network_id\":263310,\"complete\":true,\"status_code\":908}";
            default:
                return MockData.AuthLogoutIncorrectResponse().Serialize();
        }
    }

    public byte[]? GetBytes(string Url)
    {
        return Url switch
        {
            "https://rest-007.immedia-semi.com/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg?ts=1705051738&ext=" =>
                global::System.Text.Encoding.UTF8.GetBytes("fake-jpeg-bytes"),
            "https://rest-007.immedia-semi.com/api/v3/media/accounts/239361/networks/263178/catalina/321343/thumbnail/thumbnail.jpg?ts=1705099999&ext=" =>
                global::System.Text.Encoding.UTF8.GetBytes("fake-jpeg-bytes-updated"),
            "https://rest-007.immedia-semi.com/media/clip1.mp4" =>
                global::System.Text.Encoding.UTF8.GetBytes("fake-mp4-bytes"),
            "https://rest-007.immedia-semi.com/api/v1/accounts/999999/networks/1/sync_modules/500/local_storage/manifest/700/clip/request/1" =>
                global::System.Text.Encoding.UTF8.GetBytes("fake-local-mp4-bytes"),
            "https://rest-007.immedia-semi.com/api/v1/accounts/134166/networks/263178/sync_modules/390959/local_storage/manifest/710/clip/request/11" =>
                global::System.Text.Encoding.UTF8.GetBytes("fake-local-mp4-bytes-2"),
            _ => throw new NotImplementedException()
        };
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
