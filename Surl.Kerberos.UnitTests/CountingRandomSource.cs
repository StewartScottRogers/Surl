namespace Surl.Kerberos;

/// <summary>
/// A random source that hands out 0xA0, 0xA1, 0xA2 and so on, one byte after another across
/// calls, so a test knows every byte surl draws: the first four are an AP-REP's sequence number
/// <c>0xA0A1A2A3</c>.
/// </summary>
internal sealed class CountingRandomSource : IKerberosRandomSource
{
    private byte next = 0xA0;

    public void Fill(Span<byte> destination)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = next++;
        }
    }
}
