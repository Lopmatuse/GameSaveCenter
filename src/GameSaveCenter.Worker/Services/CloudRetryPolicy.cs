namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Defines the bounded retry schedule for a failed one-way backup upload.
/// Keeping the schedule deterministic makes recovery resilient across Worker restarts.
/// </summary>
public static class CloudRetryPolicy
{
    private static readonly TimeSpan[] Delays =
    {
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(12)
    };

    /// <summary>Number of automatic retry attempts after the original upload has failed.</summary>
    public static int MaximumAutomaticRetries => Delays.Length;

    /// <summary>
    /// Returns whether a failed upload has already consumed every automatic retry.
    /// Manual retry remains possible after this becomes true; only automatic scheduling stops.
    /// </summary>
    public static bool IsAutomaticRetryLimitReached(int completedAutomaticRetries)
        => completedAutomaticRetries >= MaximumAutomaticRetries;

    public static DateTime GetNextAttemptUtc(int retryAttempt, DateTime nowUtc)
    {
        if (retryAttempt < 1 || retryAttempt > MaximumAutomaticRetries)
            throw new ArgumentOutOfRangeException(nameof(retryAttempt));
        return nowUtc.ToUniversalTime().Add(Delays[retryAttempt - 1]);
    }
}
