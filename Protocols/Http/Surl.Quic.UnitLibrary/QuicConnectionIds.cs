namespace Surl.Quic;

/// <summary>The limit QUIC version 1 puts on a connection ID's length (RFC 9000 section 17.2).</summary>
internal static class QuicConnectionIds
{
    /// <summary>The longest connection ID QUIC version 1 allows, in bytes.</summary>
    public const int MaxLength = 20;

    /// <summary>Throws when a connection ID to be written is longer than 20 bytes.</summary>
    public static void RequireValidLength(ReadOnlyMemory<byte> connectionId, string parameterName) =>
        ArgumentOutOfRangeException.ThrowIfGreaterThan(connectionId.Length, MaxLength, parameterName);
}
