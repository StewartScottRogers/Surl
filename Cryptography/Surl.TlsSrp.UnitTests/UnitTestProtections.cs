namespace Surl.TlsSrp;

/// <summary>Matching AES-256 protections for tests that write records and read them back.</summary>
internal static class UnitTestProtections
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(value => (byte)(value + 1)).ToArray();
    private static readonly byte[] MacKey = Enumerable.Range(0, 20).Select(value => (byte)(value + 100)).ToArray();

    public static TlsCbcRecordProtection Aes256(bool encryptThenMac) =>
        new(TlsBulkCipher.Aes256Cbc, Key, MacKey, encryptThenMac, new UnitTestFixedRandomNumberGenerator(new byte[16]));
}
