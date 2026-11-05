namespace Surl.Conformance;

/// <summary>
/// What <see cref="CurlPortClientLocator"/> found: either a verified pinned Curl port client, or
/// the reason none is available on this machine.
/// </summary>
public sealed class CurlPortClientLocation
{
    private CurlPortClientLocation(PinnedCurlPortClient? client, string? path, CurlPortClientUnavailability unavailability, string message)
    {
        Client = client;
        Path = path;
        Unavailability = unavailability;
        Message = message;
    }

    /// <summary>
    /// Gets the verified pinned client, or <see langword="null"/> when none is available.
    /// </summary>
    public PinnedCurlPortClient? Client { get; }

    /// <summary>
    /// Gets the verified client's path with its environment variables expanded - the file that
    /// was hashed and is run - or <see langword="null"/> when none is available.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets why no client is available, or <see cref="CurlPortClientUnavailability.None"/> when one is.
    /// </summary>
    public CurlPortClientUnavailability Unavailability { get; }

    /// <summary>
    /// Gets one sentence describing the outcome, fit for an inconclusive test's message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets a value indicating whether a verified pinned client was found.
    /// </summary>
    public bool IsAvailable => Client is not null;

    /// <summary>
    /// Creates the result for a client whose file exists and matches its pin.
    /// </summary>
    /// <param name="client">The verified client.</param>
    /// <param name="path">The expanded path that was hashed.</param>
    /// <returns>An available location.</returns>
    public static CurlPortClientLocation Found(PinnedCurlPortClient client, string path) =>
        new(client, path, CurlPortClientUnavailability.None, $"{path} is the pinned Curl port client for {client.Platform}, published from commit {client.Commit}.");

    /// <summary>
    /// Creates the result for a platform with no pinned client.
    /// </summary>
    /// <param name="platform">The platform asked for.</param>
    /// <returns>An unavailable location.</returns>
    public static CurlPortClientLocation NoPinnedClient(string platform) =>
        new(null, null, CurlPortClientUnavailability.NoPinnedClientForPlatform, $"CurlPortBuilds.json pins no Curl port client for {platform}; ADR-0083 decision 2 says how one is published and pinned.");

    /// <summary>
    /// Creates the result for a pinned client whose file is not on this machine.
    /// </summary>
    /// <param name="client">The first pinned client that was looked for.</param>
    /// <param name="path">Its expanded path.</param>
    /// <returns>An unavailable location.</returns>
    public static CurlPortClientLocation FileAbsent(PinnedCurlPortClient client, string path) =>
        new(null, null, CurlPortClientUnavailability.PinnedClientFileAbsent, $"The pinned Curl port client for {client.Platform} is not installed: {path} does not exist. Publish commit {client.Commit} of the Curl port with: {client.PublishCommand}");
}
