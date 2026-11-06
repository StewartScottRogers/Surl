namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Bytes that are not a protobuf message, or a field read as a type its wire type cannot hold: a truncated
/// varint, length or fixed value, a field number of 0, an undefined wire type, an unbalanced group, a known
/// field with the wrong wire type, or a string that is not UTF-8. <see cref="GrpcStatus.FromException" /> maps it
/// to <see cref="GrpcStatus.RequestMessageDidNotParse" /> (ADR-0116 decision 2).
/// </summary>
public sealed class ProtobufFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufFormatException" /> class.
    /// </summary>
    /// <param name="message">What is wrong with the bytes.</param>
    public ProtobufFormatException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufFormatException" /> class with the failure that caused it.
    /// </summary>
    /// <param name="message">What is wrong with the bytes.</param>
    /// <param name="innerException">The failure that caused it.</param>
    public ProtobufFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
