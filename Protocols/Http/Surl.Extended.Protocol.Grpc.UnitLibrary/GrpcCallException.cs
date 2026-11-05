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

    /// <summary>The status the call ends with.</summary>
    public GrpcStatus Status { get; }
}
