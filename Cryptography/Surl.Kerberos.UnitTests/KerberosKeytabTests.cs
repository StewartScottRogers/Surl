namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="KerberosKeytab.Read" /> against MIT-format keytabs written out by hand, byte
/// by byte (ADR-0057 decisions 1 and 11): big-endian, version <c>05 02</c>, then signed
/// 32-bit-length records of component count, realm, components, name type, timestamp, 8-bit kvno,
/// enctype, key, and an optional 32-bit kvno.
/// </summary>
[TestClass]
public sealed class KerberosKeytabTests
{
    private static readonly byte[] Version = [0x05, 0x02];

    // HTTP/web@EX.COM, name type 1, timestamp 0x5F000000, kvno8 2, then the enctype and key.
    private static readonly byte[] HttpWebPrincipal =
    [
        0x00, 0x02,
        0x00, 0x06, (byte)'E', (byte)'X', (byte)'.', (byte)'C', (byte)'O', (byte)'M',
        0x00, 0x04, (byte)'H', (byte)'T', (byte)'T', (byte)'P',
        0x00, 0x03, (byte)'w', (byte)'e', (byte)'b',
        0x00, 0x00, 0x00, 0x01,
        0x5F, 0x00, 0x00, 0x00,
        0x02,
    ];

    private static readonly KerberosPrincipalName HttpWeb = new("EX.COM", ["HTTP", "web"]);

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, 16, DisplayName = "17")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, 32, DisplayName = "18")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, 16, DisplayName = "19")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, 32, DisplayName = "20")]
    public void Read_EntryOfEachAcceptedEnctype_GivesItsPrincipalKvnoEnctypeAndKey(KerberosEncryptionType encryptionType, int keyLength)
    {
        byte[] key = Enumerable.Range(1, keyLength).Select(value => (byte)value).ToArray();
        byte[] bytes = [.. Version, .. Record([.. HttpWebPrincipal, 0x00, (byte)encryptionType, 0x00, (byte)keyLength, .. key])];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.IsNull(result.MalformedOffset);
        Assert.IsEmpty(result.SkippedEntries);
        KerberosKeytabEntry entry = result.Keytab!.Entries.Single();
        Assert.AreEqual(HttpWeb, entry.Principal);
        Assert.AreEqual(2U, entry.KeyVersionNumber);
        Assert.AreEqual(encryptionType, entry.EncryptionType);
        CollectionAssert.AreEqual(key, entry.Key.ToArray());
    }

    [TestMethod]
    public void Read_AllFourEnctypesInOneFile_GivesFourEntriesInFileOrder()
    {
        byte[] bytes =
        [
            .. Version,
            .. Aes128Record(),
            .. Record([.. HttpWebPrincipal, 0x00, 18, 0x00, 32, .. new byte[32]]),
            .. Record([.. HttpWebPrincipal, 0x00, 19, 0x00, 16, .. new byte[16]]),
            .. Record([.. HttpWebPrincipal, 0x00, 20, 0x00, 32, .. new byte[32]]),
        ];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        CollectionAssert.AreEqual(
            new[] { 17, 18, 19, 20 },
            result.Keytab!.Entries.Select(entry => (int)entry.EncryptionType).ToArray());
    }

    [TestMethod]
    public void Read_DeletedEntry_IsSkipped()
    {
        byte[] bytes = [.. Version, 0xFF, 0xFF, 0xFF, 0xFA, 1, 2, 3, 4, 5, 6, .. Aes128Record()];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.HasCount(1, result.Keytab!.Entries);
        Assert.IsEmpty(result.SkippedEntries);
    }

    [TestMethod]
    public void Read_Trailing32BitKvno_OverridesThe8BitOne()
    {
        byte[] bytes = [.. Version, .. Record([.. HttpWebPrincipal, 0x00, 17, 0x00, 16, .. new byte[16], 0x00, 0x00, 0x01, 0x05])];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(261U, result.Keytab!.Entries.Single().KeyVersionNumber);
    }

    [TestMethod]
    public void Read_Trailing32BitKvnoOfZero_IsIgnored()
    {
        byte[] bytes = [.. Version, .. Record([.. HttpWebPrincipal, 0x00, 17, 0x00, 16, .. new byte[16], 0x00, 0x00, 0x00, 0x00])];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(2U, result.Keytab!.Entries.Single().KeyVersionNumber);
    }

    [TestMethod]
    public void Read_Rc4HmacEntry_IsSkippedAndReportedWithItsEnctypeAndPrincipal()
    {
        byte[] bytes = [.. Version, .. Record([.. HttpWebPrincipal, 0x00, 23, 0x00, 16, .. new byte[16]]), .. Aes128Record()];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.HasCount(1, result.Keytab!.Entries);
        KerberosKeytabSkippedEntry skipped = result.SkippedEntries.Single();
        Assert.AreEqual(23, skipped.EncryptionTypeNumber);
        Assert.AreEqual(HttpWeb, skipped.Principal);
    }

    [TestMethod]
    public void Read_ZeroLengthRecord_EndsTheFile()
    {
        byte[] bytes = [.. Version, .. Aes128Record(), 0x00, 0x00, 0x00, 0x00, 0xDE, 0xAD];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.HasCount(1, result.Keytab!.Entries);
    }

    [TestMethod]
    public void Read_VersionOnly_GivesAnEmptyKeytab()
    {
        KerberosKeytabReadResult result = KerberosKeytab.Read(Version);

        Assert.IsEmpty(result.Keytab!.Entries);
        Assert.IsNull(result.MalformedOffset);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x01 }, 0, DisplayName = "version 0x0501")]
    [DataRow(new byte[] { }, 0, DisplayName = "empty")]
    [DataRow(new byte[] { 0x05 }, 0, DisplayName = "half a version")]
    [DataRow(new byte[] { 0x05, 0x02, 0x00, 0x00 }, 2, DisplayName = "half a record length")]
    [DataRow(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x02 }, 2, DisplayName = "record running past the end")]
    [DataRow(new byte[] { 0x05, 0x02, 0xFF, 0xFF, 0xFF, 0xF0, 0x00 }, 2, DisplayName = "deleted record running past the end")]
    [DataRow(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00 }, 6, DisplayName = "no components")]
    [DataRow(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x09 }, 8, DisplayName = "realm running past the record")]
    [DataRow(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00 }, 10, DisplayName = "component length cut short")]
    public void Read_MalformedBytes_ReportsTheOffsetOfTheFieldThatCannotBeRead(byte[] bytes, int offset)
    {
        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(offset, result.MalformedOffset);
        Assert.IsNull(result.Keytab);
        Assert.IsEmpty(result.SkippedEntries);
    }

    [TestMethod]
    public void Read_EntryTruncatedBeforeItsKey_IsMalformedAtTheKeyLengthsOffset()
    {
        byte[] record = [.. HttpWebPrincipal, 0x00, 17, 0x00, 16, .. new byte[8]];
        byte[] bytes = [.. Version, .. Record(record)];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(2 + 4 + HttpWebPrincipal.Length + 2, result.MalformedOffset);
    }

    [TestMethod]
    public void Read_KeyNotAsLongAsItsEnctypes_IsMalformedAtTheKeyLengthsOffset()
    {
        byte[] bytes = [.. Version, .. Record([.. HttpWebPrincipal, 0x00, 18, 0x00, 16, .. new byte[16]])];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(2 + 4 + HttpWebPrincipal.Length + 2, result.MalformedOffset);
    }

    [TestMethod]
    public void Read_SecondEntryTruncated_ReportsNoSkippedEntries()
    {
        byte[] bytes = [.. Version, .. Record([.. HttpWebPrincipal, 0x00, 23, 0x00, 16, .. new byte[16]]), 0x00, 0x00, 0x00, 0x40];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(2 + 4 + HttpWebPrincipal.Length + 4 + 16, result.MalformedOffset);
        Assert.IsEmpty(result.SkippedEntries);
    }

    [TestMethod]
    public void Read_NonAsciiRealm_IsDecodedAsUtf8()
    {
        byte[] bytes =
        [
            .. Version,
            .. Record([0x00, 0x01, 0x00, 0x02, 0xC3, 0x89, 0x00, 0x01, (byte)'u', 0, 0, 0, 1, 0, 0, 0, 0, 1, 0x00, 17, 0x00, 16, .. new byte[16]]),
        ];

        KerberosKeytabReadResult result = KerberosKeytab.Read(bytes);

        Assert.AreEqual(new KerberosPrincipalName("É", ["u"]), result.Keytab!.Entries.Single().Principal);
    }

    [TestMethod]
    public void FindKey_KvnoGiven_FindsThatKvnoOnly()
    {
        KerberosKeytab keytab = new([Entry(HttpWeb, 2, 0x02), Entry(HttpWeb, 5, 0x05), Entry(HttpWeb, 3, 0x03)]);

        Assert.AreEqual(3U, keytab.FindKey(HttpWeb, 17, 3)!.KeyVersionNumber);
        Assert.IsNull(keytab.FindKey(HttpWeb, 17, 4));
    }

    [TestMethod]
    public void FindKey_KvnoAbsent_FindsTheHighestForTheEnctype()
    {
        KerberosKeytab keytab = new([Entry(HttpWeb, 2, 0x02), Entry(HttpWeb, 5, 0x05), Entry(HttpWeb, 3, 0x03)]);

        Assert.AreEqual(5U, keytab.FindKey(HttpWeb, 17, null)!.KeyVersionNumber);
        Assert.IsNull(keytab.FindKey(HttpWeb, 18, null));
    }

    [TestMethod]
    public void FindKey_ServiceAndRealmInAnotherCase_StillMatch()
    {
        KerberosKeytab keytab = new([Entry(HttpWeb, 2, 0x02)]);

        Assert.IsNotNull(keytab.FindKey(new KerberosPrincipalName("ex.com", ["http", "web"]), 17, 2));
    }

    [TestMethod]
    [DataRow("EX.COM", new[] { "HTTP", "WEB" }, DisplayName = "host in another case")]
    [DataRow("EX.COM", new[] { "HTTP" }, DisplayName = "fewer components")]
    [DataRow("EX.ORG", new[] { "HTTP", "web" }, DisplayName = "another realm")]
    public void FindKey_OtherPrincipal_FindsNothing(string realm, string[] components)
    {
        KerberosKeytab keytab = new([Entry(HttpWeb, 2, 0x02)]);

        Assert.IsNull(keytab.FindKey(new KerberosPrincipalName(realm, components), 17, 2));
    }

    private static KerberosKeytabEntry Entry(KerberosPrincipalName principal, uint kvno, byte keyByte) =>
        new(principal, kvno, KerberosEncryptionType.Aes128CtsHmacSha196, Enumerable.Repeat(keyByte, 16).ToArray());

    private static byte[] Aes128Record() => Record([.. HttpWebPrincipal, 0x00, 17, 0x00, 16, .. new byte[16]]);

    private static byte[] Record(byte[] entry) =>
        [(byte)(entry.Length >> 24), (byte)(entry.Length >> 16), (byte)(entry.Length >> 8), (byte)entry.Length, .. entry];
}
