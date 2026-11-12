using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Draws the <c>x-amz-request-id</c> of a reply: 16 upper-case hex digits from an identifier source,
/// so a seeded exchange answers the same ID every run (ADR-0124, decision 6).
/// </summary>
internal static class S3RequestId
{
    private const int ByteCount = 8;

    /// <summary>
    /// Draws a request ID.
    /// </summary>
    /// <param name="identifiers">Where the ID's bytes come from.</param>
    /// <returns>16 upper-case hex digits.</returns>
    public static string Draw(IIdentifierRandom identifiers)
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        identifiers.NextBytes(bytes);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// The source a server uses for a connection refusal when none is given: the bytes come from
    /// <see cref="RandomNumberGenerator"/>, so unseeded servers answer different IDs.
    /// </summary>
    internal sealed class Unseeded : IIdentifierRandom
    {
        /// <inheritdoc/>
        public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
    }
}
