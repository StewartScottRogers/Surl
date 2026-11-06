namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// One API the broker serves and the versions of it served (ADR-0105 decision 1), with the
/// first version the Kafka protocol guide makes flexible.
/// </summary>
/// <param name="ApiKey">The API.</param>
/// <param name="MinVersion">The lowest version served.</param>
/// <param name="MaxVersion">The highest version served.</param>
/// <param name="FirstFlexibleVersion">
/// The first version the guide makes flexible (compact types and tagged fields), or
/// <see langword="null"/> when no version of the API is.
/// </param>
public sealed record KafkaApiVersionRange(KafkaApiKey ApiKey, short MinVersion, short MaxVersion, short? FirstFlexibleVersion)
{
    /// <summary>Whether <paramref name="version"/> is served.</summary>
    /// <param name="version">The requested version.</param>
    /// <returns>True when it lies between <see cref="MinVersion"/> and <see cref="MaxVersion"/>.</returns>
    public bool Serves(short version) => version >= MinVersion && version <= MaxVersion;

    /// <summary>Whether <paramref name="version"/> is flexible.</summary>
    /// <param name="version">The requested version.</param>
    /// <returns>True from <see cref="FirstFlexibleVersion"/> on.</returns>
    public bool IsFlexible(short version) => version >= FirstFlexibleVersion;

    /// <summary>
    /// The request header version a request at <paramref name="version"/> carries: 2 when the
    /// version is flexible, 1 otherwise (the guide, "Request and Response Headers").
    /// </summary>
    /// <param name="version">The requested version.</param>
    /// <returns>1 or 2.</returns>
    public short RequestHeaderVersion(short version) => IsFlexible(version) ? (short)2 : (short)1;

    /// <summary>
    /// The response header version an answer at <paramref name="version"/> carries: 1 when the
    /// version is flexible, 0 otherwise, and always 0 for ApiVersions, which the guide keeps at
    /// response header v0 so that a client can read it whatever version it asked for.
    /// </summary>
    /// <param name="version">The requested version.</param>
    /// <returns>0 or 1.</returns>
    public short ResponseHeaderVersion(short version) =>
        ApiKey != KafkaApiKey.ApiVersions && IsFlexible(version) ? (short)1 : (short)0;
}
