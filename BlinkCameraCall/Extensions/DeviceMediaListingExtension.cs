namespace BlinkCameraCall.Extensions;

internal static class DeviceMediaListingExtension
{
    internal static string ToListingString(this DeviceMediaListing listing)
    {
        if (listing.Entries.Count == 0)
            return "No media found for this device.";

        return string.Join(Environment.NewLine, listing.OrderedLines());
    }

    internal static IEnumerable<string> IndentedLines(this DeviceMediaListing listing, string indent) =>
        listing.OrderedLines().Select(line => indent + line);

    private static IEnumerable<string> OrderedLines(this DeviceMediaListing listing)
    {
        var current = listing.Entries.Where(e => e.Kind == DeviceMediaKind.Current);
        var rest = listing.Entries.Where(e => e.Kind != DeviceMediaKind.Current).OrderByDescending(e => e.Timestamp);

        return current.Concat(rest).Select(e => $"{e.Name}  [{e.Kind.ToString().ToLowerInvariant()}]");
    }
}
