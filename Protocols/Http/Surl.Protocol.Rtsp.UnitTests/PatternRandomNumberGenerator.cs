using System.Security.Cryptography;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// A <see cref="RandomNumberGenerator"/> that fills every request with <c>01 23 45 67 89 AB CD
/// EF</c> repeated, so a session drawn from it has the ID <see cref="SessionId"/>, the SSRC
/// <see cref="Ssrc"/>, the first sequence number <see cref="FirstSequenceNumber"/> and the first
/// timestamp <see cref="FirstTimestamp"/>.
/// </summary>
internal sealed class PatternRandomNumberGenerator : RandomNumberGenerator
{
    public const string SessionId = "0123456789ABCDEF";

    public const uint Ssrc = 0x0123_4567;

    public const ushort FirstSequenceNumber = 0x89AB;

    public const uint FirstTimestamp = 0xCDEF_0123;

    private static readonly byte[] Pattern = [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF];

    public override void GetBytes(byte[] data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = Pattern[index % Pattern.Length];
        }
    }
}
