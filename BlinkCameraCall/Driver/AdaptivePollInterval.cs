namespace BlinkCameraCall.Driver;

public sealed class AdaptivePollInterval(TimeSpan initial)
{
    public TimeSpan Current { get; private set; } = initial;

    // totalRoundElapsed: the full round's own wall-clock time (one cloud
    // check for WATCH; the sum of every sync module's check for LOCALWATCH).
    // slowestItemElapsed: the slowest single item's own time within the
    // round (equal to totalRoundElapsed when a round is only one item,
    // which is always true for WATCH).
    // Returns the new interval if the round was slower than Current (and
    // updates Current to it); returns null and leaves Current unchanged
    // otherwise.
    public TimeSpan? ReportRound(TimeSpan totalRoundElapsed, TimeSpan slowestItemElapsed)
    {
        if (totalRoundElapsed <= Current)
            return null;

        var candidate = slowestItemElapsed + TimeSpan.FromSeconds(1);
        if (candidate == Current)
            return null;

        Current = candidate;
        return candidate;
    }
}
