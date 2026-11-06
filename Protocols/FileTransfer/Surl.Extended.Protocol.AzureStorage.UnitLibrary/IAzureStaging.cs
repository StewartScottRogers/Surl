namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One upload the <see cref="AzureStagingArea"/> holds: when it was last touched, whether it holds
/// staged bytes, and disposing it discards them.
/// </summary>
internal interface IAzureStaging : IAsyncDisposable
{
    /// <summary>
    /// When a request last wrote to the staging.
    /// </summary>
    DateTimeOffset LastTouched { get; }

    /// <summary>
    /// Whether the staging holds bytes not yet committed; one that does not may be forgotten to
    /// make room for another.
    /// </summary>
    bool HoldsStagedBytes { get; }
}
