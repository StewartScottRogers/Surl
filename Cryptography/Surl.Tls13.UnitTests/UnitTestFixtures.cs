namespace Surl.Tls13;

/// <summary>
/// Reads the recorded ClientHellos embedded in this assembly from Fixtures/ClientHello. Each is
/// the clienthello.bin that Record-CurlExchange.ps1 -QuicInitial (BL-363) decrypted from a
/// pinned upstream curl's Initial packets on 2026-11-04 (BL-375's Notes say how).
/// </summary>
internal static class UnitTestFixtures
{
    public static byte[] Read(string name)
    {
        using var stream = typeof(UnitTestFixtures).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"No embedded fixture {name}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
