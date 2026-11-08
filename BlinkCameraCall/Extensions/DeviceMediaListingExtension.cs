namespace BlinkCameraCall.Extensions;

internal static class DeviceMediaListingExtension
{
    internal static string ToListingString(this DeviceMediaListing listing)
    {
        if (listing.Entries.Count == 0)
            return "No media found for this device.";

        var current = listing.Entries.Where(e => e.Kind == DeviceMediaKind.Current);
        var rest = listing.Entries.Where(e => e.Kind != DeviceMediaKind.Current).OrderByDescending(e => e.Timestamp);

        return string.Join(Environment.NewLine, current.Concat(rest)
            .Select(e => $"{e.Name}  [{e.Kind.ToString().ToLowerInvariant()}]"));
    }
}
