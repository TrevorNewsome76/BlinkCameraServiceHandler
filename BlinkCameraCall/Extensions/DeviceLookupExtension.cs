using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Extensions;

public static class DeviceLookupExtension
{
    public static IHomeScreenCamera? FindCameraByName(this ISessionDetails session, string name) =>
        session.Cameras?.SingleOrNull(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public static IHomeScreenDoorbells? FindDoorbellByName(this ISessionDetails session, string name) =>
        session.Doorbells?.SingleOrNull(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

    public static IHomeScreenSyncModule? FindSyncModuleByName(this ISessionDetails session, string name) =>
        session.SyncModules?.SingleOrNull(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static T? SingleOrNull<T>(this List<T> items, Func<T, bool> predicate) where T : class
    {
        var matches = items.Where(predicate).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
