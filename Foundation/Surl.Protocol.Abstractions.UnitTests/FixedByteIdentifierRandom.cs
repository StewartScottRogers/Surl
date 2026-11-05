namespace Surl.Protocol.Abstractions;

/// <summary>
/// A test's <see cref="IIdentifierRandom"/> that fills every destination with <c>0x2A</c>.
/// </summary>
internal sealed class FixedByteIdentifierRandom : IIdentifierRandom
{
    public void NextBytes(Span<byte> destination) => destination.Fill(0x2A);
}
