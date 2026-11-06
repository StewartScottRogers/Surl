using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>
/// A random number generator that hands out the scripted byte strings first, each to exactly one
/// request of its length, then real random bytes, so a test can inject a server random and a
/// private key.
/// </summary>
internal sealed class UnitTestScriptedRandom(params byte[][] scripted) : RandomNumberGenerator
{
    private readonly Queue<byte[]> scripted = new(scripted);

    public override void GetBytes(byte[] data)
    {
        if (scripted.TryDequeue(out var next))
        {
            Assert.HasCount(data.Length, next);
            next.CopyTo(data, 0);
            return;
        }

        Fill(data);
    }
}
