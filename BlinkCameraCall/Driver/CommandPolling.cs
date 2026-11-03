using BlinkCommon.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.System;

namespace BlinkCameraCall.Driver;

public static class CommandPolling
{
    public static ICommandStatusResponse? WaitForCommandComplete(
        IApiMethods api,
        string commandStatusUrl,
        TimeSpan? pollInterval = null,
        int maxRetries = 120)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var status = (api.Get(commandStatusUrl) ?? string.Empty).Deserialize<ICommandStatusResponse>();

            if (status.Complete)
                return status;

            if (status.Status_Code != 0 && status.Status_Code != 908)
                return null;

            Thread.Sleep(interval);
        }

        return null;
    }
}
