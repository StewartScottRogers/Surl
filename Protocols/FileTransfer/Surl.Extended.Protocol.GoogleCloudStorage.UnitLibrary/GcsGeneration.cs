namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// An object's generation (ADR-0110, decision 1.1; ADR-0100, decision 2.3): a time in microseconds
/// since the Unix epoch - the upload time for an object put over <c>gs</c>, the last-write time for
/// any other file.
/// </summary>
internal static class GcsGeneration
{
    /// <summary>
    /// The last generation that stands for a time <see cref="DateTimeOffset"/> can hold.
    /// </summary>
    public static readonly long LastTimeInMicroseconds = (DateTime.MaxValue.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMicrosecond;

    /// <summary>
    /// The time a generation stands for.
    /// </summary>
    /// <param name="generation">Microseconds since the Unix epoch, from 0 to <see cref="LastTimeInMicroseconds"/>.</param>
    /// <returns>The time, in UTC.</returns>
    public static DateTimeOffset TimeOf(long generation) =>
        new(DateTime.UnixEpoch.Ticks + (generation * TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);

    /// <summary>
    /// The generation a time stands for.
    /// </summary>
    /// <param name="time">The upload or last-write time.</param>
    /// <returns>Microseconds since the Unix epoch.</returns>
    public static long Of(DateTimeOffset time) =>
        (time.UtcTicks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMicrosecond;
}
