namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Ends a call with a chosen <see cref="GrpcStatus" />: the framing, compression and <c>grpc-timeout</c>
/// readers throw it for each failure ADR-0116 maps to a status, and the server sends that status.
/// </summary>
public sealed class GrpcCallException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcCallException" /> class.
    /// </summary>
    /// <param name="status">The status the call ends with.</param>
    public GrpcCallException(GrpcStatus status)
        : base(status.Message)
    {
        Status = status;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcCallException" /> class with the failure that caused it.
    /// </summary>
    /// <param name="status">The status the call ends with.</param>
    /// <param name="innerException">The failure that caused it.</param>
    public GrpcCallException(GrpcStatus status, Exception innerException)
        : base(status.Message, innerException)
    {
        Status = status;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcCallException" /> class whose answer carries
    /// <paramref name="trailerMetadata" /> beside the status: an <c>UNAUTHENTICATED</c> call's
    /// <c>www-authenticate</c> values (ADR-0116 decision 7).
    /// </summary>
    /// <param name="status">The status the call ends with.</param>
    /// <param name="trailerMetadata">The metadata sent after the status fields, names lowercase.</param>
    public GrpcCallException(GrpcStatus status, IReadOnlyList<KeyValuePair<string, string>> trailerMetadata)
        : base(status.Message)
    {
        Status = status;
        TrailerMetadata = trailerMetadata;
    }

    /// <summary>The status the call ends with.</summary>
    public GrpcStatus Status { get; }

    /// <summary>The metadata the answer carries after its status fields; none unless the constructor was given some.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> TrailerMetadata { get; } = [];
}
