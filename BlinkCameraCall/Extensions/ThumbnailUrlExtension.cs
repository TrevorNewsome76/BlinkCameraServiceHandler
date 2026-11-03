namespace BlinkCameraCall.Extensions;

public static class ThumbnailUrlExtension
{
    public static string BuildThumbnailUrl(string thumbnailField, string tier, long accountId, long networkId, long deviceId, bool isDoorbell)
    {
        var tierBase = new Uri($"https://rest-{tier}.immedia-semi.com");

        string relativeOrAbsolute;
        if (long.TryParse(thumbnailField, out var ts))
        {
            var segment = isDoorbell ? "doorbell" : "camera";
            relativeOrAbsolute = $"/api/v3/media/accounts/{accountId}/networks/{networkId}/{segment}/{deviceId}/thumbnail/thumbnail.jpg?ts={ts}&ext=";
        }
        else
        {
            relativeOrAbsolute = thumbnailField.EndsWith("&ext=") ? thumbnailField : $"{thumbnailField}.jpg";
        }

        return new Uri(tierBase, relativeOrAbsolute).ToString();
    }
}
