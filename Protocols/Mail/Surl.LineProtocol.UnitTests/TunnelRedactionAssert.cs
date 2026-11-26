using System.Buffers;

namespace Surl.LineProtocol;

/// <summary>Reads what a tunnel's binary login redactor logs of the bytes a client sends.</summary>
internal static class TunnelRedactionAssert
{
    /// <summary>
    /// What the redactor the first byte chooses logs of <paramref name="chunks"/>, with the bytes held
    /// at the end.
    /// </summary>
    public static byte[] Logged(params byte[][] chunks)
    {
        var redactor = TunnelBinaryLoginRedactors.ForFirstByte(chunks.First(chunk => chunk.Length > 0)[0])!;
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
            redactor.ObserveSent([0x20, 0x02, 0x00, 0x00]);
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    /// <summary>Asserts the log of <paramref name="sent"/> split in two at every byte, and sent one byte at a time.</summary>
    public static void AssertLoggedHoweverSplit(byte[] expected, byte[] sent)
    {
        for (var split = 0; split < sent.Length; split++)
        {
            CollectionAssert.AreEqual(expected, Logged(sent[..split], sent[split..]), $"split at {split}");
        }

        CollectionAssert.AreEqual(expected, Logged([.. sent.SelectMany(value => new byte[][] { [], [value] })]));
    }
}
