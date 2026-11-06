namespace Surl.Cryptography.MlKem;

/// <summary>
/// The Keccak-p[1600, 24] permutation, Keccak-f[1600], of FIPS 202 section 3: theta, rho, pi, chi
/// and iota on a 5 by 5 state of 64-bit lanes, lane (x, y) at index x + 5y. Hand-built because the
/// base class library's SHA-3 is missing on macOS (ADR-0082 decision 2).
/// </summary>
internal static class Keccak
{
    /// <summary>The number of 64-bit lanes in the state.</summary>
    public const int LaneCount = 25;

    private const int RoundCount = 24;

    /// <summary>The round constants of iota (FIPS 202 section 3.2.5), one per round.</summary>
    private static readonly ulong[] RoundConstants =
    [
        0x0000000000000001, 0x0000000000008082, 0x800000000000808A, 0x8000000080008000,
        0x000000000000808B, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
        0x000000000000008A, 0x0000000000000088, 0x0000000080008009, 0x000000008000000A,
        0x000000008000808B, 0x800000000000008B, 0x8000000000008089, 0x8000000000008003,
        0x8000000000008002, 0x8000000000000080, 0x000000000000800A, 0x800000008000000A,
        0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008,
    ];

    /// <summary>The lanes rho and pi visit in turn, starting from lane (1, 0) (FIPS 202 sections 3.2.2 and 3.2.3).</summary>
    private static readonly int[] PiLanes =
        [10, 7, 11, 17, 18, 3, 5, 16, 8, 21, 24, 4, 15, 23, 19, 13, 12, 2, 20, 14, 22, 9, 6, 1];

    /// <summary>The rho rotation of each lane in <see cref="PiLanes"/>'s order.</summary>
    private static readonly int[] RhoOffsets =
        [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 2, 14, 27, 41, 56, 8, 25, 43, 62, 18, 39, 61, 20, 44];

    /// <summary>Applies Keccak-f[1600] to <paramref name="state"/> in place.</summary>
    /// <param name="state">The 25 lanes of the state.</param>
    public static void Permute(Span<ulong> state)
    {
        Span<ulong> column = stackalloc ulong[5];
        for (var round = 0; round < RoundCount; round++)
        {
            Theta(state, column);
            RhoAndPi(state);
            Chi(state, column);
            state[0] ^= RoundConstants[round];
        }
    }

    private static void Theta(Span<ulong> state, Span<ulong> parity)
    {
        for (var x = 0; x < 5; x++)
        {
            parity[x] = state[x] ^ state[x + 5] ^ state[x + 10] ^ state[x + 15] ^ state[x + 20];
        }

        for (var x = 0; x < 5; x++)
        {
            var effect = parity[(x + 4) % 5] ^ ulong.RotateLeft(parity[(x + 1) % 5], 1);
            for (var y = 0; y < LaneCount; y += 5)
            {
                state[x + y] ^= effect;
            }
        }
    }

    private static void RhoAndPi(Span<ulong> state)
    {
        var current = state[1];
        for (var step = 0; step < PiLanes.Length; step++)
        {
            var lane = PiLanes[step];
            (state[lane], current) = (ulong.RotateLeft(current, RhoOffsets[step]), state[lane]);
        }
    }

    private static void Chi(Span<ulong> state, Span<ulong> row)
    {
        for (var y = 0; y < LaneCount; y += 5)
        {
            state.Slice(y, 5).CopyTo(row);
            for (var x = 0; x < 5; x++)
            {
                state[x + y] = row[x] ^ (~row[(x + 1) % 5] & row[(x + 2) % 5]);
            }
        }
    }
}
