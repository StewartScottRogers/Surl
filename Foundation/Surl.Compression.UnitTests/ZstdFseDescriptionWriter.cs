namespace Surl.Compression;

/// <summary>
/// Writes an FSE table description as RFC 8878, section 4.1.1 lays it out: the accuracy log minus 5
/// in 4 bits, then each count plus one in a field one bit narrower when its value is small enough,
/// and after each zero count 2-bit repeat fields giving how many zero counts follow (3 meaning three
/// and another field).
/// </summary>
internal static class ZstdFseDescriptionWriter
{
    /// <summary>The description of <paramref name="counts"/> (-1 for "less than one") at <paramref name="accuracyLog"/>.</summary>
    public static byte[] Write(short[] counts, int accuracyLog)
    {
        List<bool> bits = [];
        ZstdForwardBits.Put(bits, accuracyLog - 5, 4);
        int threshold = 1 << accuracyLog;
        int remaining = threshold + 1;
        int width = accuracyLog + 1;
        int symbol = 0;
        bool previousWasZero = false;
        while (remaining > 1)
        {
            if (previousWasZero)
            {
                symbol = WriteZeroRun(bits, counts, symbol);
            }

            int count = counts[symbol++];
            int value = count + 1;
            int maximum = (2 * threshold) - 1 - remaining;
            if (value >= threshold)
            {
                value += maximum;
            }

            ZstdForwardBits.Put(bits, value, value < maximum ? width - 1 : width);
            remaining -= Math.Abs(count);
            previousWasZero = count == 0;
            while (remaining < threshold)
            {
                width--;
                threshold >>= 1;
            }
        }

        return ZstdForwardBits.Pack(bits);
    }

    private static int WriteZeroRun(List<bool> bits, short[] counts, int symbol)
    {
        int run = 0;
        while (counts[symbol + run] == 0)
        {
            run++;
        }

        int next = symbol + run;
        for (; run >= 3; run -= 3)
        {
            ZstdForwardBits.Put(bits, 3, 2);
        }

        ZstdForwardBits.Put(bits, run, 2);
        return next;
    }
}
