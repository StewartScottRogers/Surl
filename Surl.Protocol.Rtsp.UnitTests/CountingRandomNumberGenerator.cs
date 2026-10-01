using System.Security.Cryptography;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// A <see cref="RandomNumberGenerator"/> that fills its first request with <c>01</c> bytes, its
/// second with <c>02</c>, and so on, so each session set up draws an ID of its own:
/// <see cref="FirstSessionId"/>, then <see cref="SecondSessionId"/>.
/// </summary>
internal sealed class CountingRandomNumberGenerator : RandomNumberGenerator
{
    public const string FirstSessionId = "0101010101010101";

    public const string SecondSessionId = "0202020202020202";

    private byte requests;

    public override void GetBytes(byte[] data)
    {
        requests++;
        Array.Fill(data, requests);
    }
}
