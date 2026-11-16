using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A <see cref="RandomNumberGenerator"/> over an instance's <see cref="IIdentifierRandom"/>, so a TLS-SRP
/// salt a test adds is the same every run under <c>--random-seed</c> (ADR-0170 decision 3, ADR-0124 decision 6).
/// </summary>
/// <param name="source">Where the bytes come from.</param>
internal sealed class IdentifierRandomNumberGenerator(IIdentifierRandom source) : RandomNumberGenerator
{
    /// <inheritdoc/>
    public override void GetBytes(byte[] data) => source.NextBytes(data);

    /// <inheritdoc/>
    public override void GetBytes(Span<byte> data) => source.NextBytes(data);
}
