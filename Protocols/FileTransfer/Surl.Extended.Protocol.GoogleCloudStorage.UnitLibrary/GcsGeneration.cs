namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// An object's generation (ADR-0110, decision 1.1; ADR-0100, decision 2.3): a time in microseconds
/// since the Unix epoch - the upload time for an object put over <c>gs</c>, the last-write time for
/// any other file.
/// </summary>
internal static class GcsGeneration
{
    /// <summary>
    /// The metageneration every object reports: no request changes metadata alone.
    /// </summary>
    public const long Metageneration = 1;

    /// <summary>
    /// The generation a time stands for.
    /// </summary>
    /// <param name="time">The upload or last-write time.</param>
    /// <returns>Microseconds since the Unix epoch.</returns>
    public static long Of(DateTimeOffset time) =>
        (time.UtcTicks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMicrosecond;
}
