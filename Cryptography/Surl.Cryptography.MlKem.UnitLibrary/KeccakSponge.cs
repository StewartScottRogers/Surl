using System.Buffers.Binary;

namespace Surl.Cryptography.MlKem;

/// <summary>
/// The sponge construction of FIPS 202 section 4 over <see cref="Keccak"/>, absorbing one whole
/// message with the SHA-3 or SHAKE domain bits and pad10*1 (section 5.1, Table 6 of section B.2),
/// then squeezing as many bytes as asked, in as many calls as the caller likes.
/// </summary>
internal sealed class KeccakSponge
{
    private readonly ulong[] state = new ulong[Keccak.LaneCount];
    private readonly int rate;
    private int squeezed;

    /// <summary>Absorbs <paramref name="message"/>, then the domain bits and the padding.</summary>
    /// <param name="rate">The rate in bytes: 200 minus twice the capacity's security level.</param>
    /// <param name="domain">The domain bits with the first padding bit: 0x06 for SHA-3, 0x1F for SHAKE.</param>
    /// <param name="message">The whole message.</param>
    public KeccakSponge(int rate, byte domain, ReadOnlySpan<byte> message)
    {
        this.rate = rate;
        while (message.Length >= rate)
        {
            AbsorbBlock(message[..rate]);
            message = message[rate..];
        }

        Span<byte> last = stackalloc byte[rate];
        last.Clear();
        message.CopyTo(last);
        last[message.Length] ^= domain;
        last[rate - 1] ^= 0x80;
        AbsorbBlock(last);
    }

    /// <summary>Writes the next <paramref name="output"/>.Length bytes of the output.</summary>
    public void Squeeze(Span<byte> output)
    {
        for (var index = 0; index < output.Length; index++)
        {
            if (squeezed == rate)
            {
                Keccak.Permute(state);
                squeezed = 0;
            }

            output[index] = (byte)(state[squeezed >> 3] >> ((squeezed & 7) * 8));
            squeezed++;
        }
    }

    private void AbsorbBlock(ReadOnlySpan<byte> block)
    {
        for (var lane = 0; lane < rate / 8; lane++)
        {
            state[lane] ^= BinaryPrimitives.ReadUInt64LittleEndian(block[(lane * 8)..]);
        }

        Keccak.Permute(state);
    }
}
