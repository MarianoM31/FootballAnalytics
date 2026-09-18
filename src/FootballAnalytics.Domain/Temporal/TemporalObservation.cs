namespace FootballAnalytics.Domain.Temporal;

public static class TemporalObservation
{
    public static DateTimeOffset ResolveAvailableAtUtc(DateTimeOffset? availableAtUtc, DateTimeOffset ingestedAtUtc) =>
        availableAtUtc ?? ingestedAtUtc;

    public static void Validate(DateTimeOffset? observedAtUtc, DateTimeOffset availableAtUtc, DateTimeOffset ingestedAtUtc)
    {
        EnsureUtc(availableAtUtc, nameof(availableAtUtc));
        EnsureUtc(ingestedAtUtc, nameof(ingestedAtUtc));
        if (observedAtUtc.HasValue)
        {
            EnsureUtc(observedAtUtc.Value, nameof(observedAtUtc));
        }

        if (availableAtUtc > ingestedAtUtc)
        {
            throw new ArgumentException("AvailableAtUtc cannot be after IngestedAtUtc.");
        }
    }

    public static bool IsAvailableAsOf(DateTimeOffset availableAtUtc, DateTimeOffset ingestedAtUtc, DateTimeOffset cutoffUtc)
    {
        EnsureUtc(cutoffUtc, nameof(cutoffUtc));
        return availableAtUtc <= cutoffUtc && ingestedAtUtc <= cutoffUtc;
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamps must be expressed in UTC.", parameterName);
        }
    }
}
