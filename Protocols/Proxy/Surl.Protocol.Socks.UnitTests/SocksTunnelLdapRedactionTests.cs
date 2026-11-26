using System.Buffers;

namespace Surl.Protocol.Socks;

/// <summary>LDAP bind secrets through a granted SOCKS tunnel, as the exchange log shows them (ADR-0182, amendment 10).</summary>
[TestClass]
public sealed class SocksTunnelLdapRedactionTests
{
    // What pinned upstream curl 8.21.0 (WinLDAP) sent first for `-u alice:secret ldap://...`
    // (Surl.Protocol.Ldap.UnitTests/Fixtures/simple-bind-base-search): the bind, then the unbind.
    private static readonly byte[] CurlBind =
    [
        0x30, 0x84, 0, 0, 0, 0x1B, 0x02, 0x01, 0x01, 0x60, 0x84, 0, 0, 0, 0x12, 0x02, 0x01, 0x03,
        0x04, 0x05, .. "alice"u8, 0x80, 0x06, .. "secret"u8,
    ];

    private static readonly byte[] CurlBindMasked = [.. CurlBind[..^8], 0x80, 0x04, .. "****"u8];

    private static readonly byte[] CurlUnbind = [0x30, 0x84, 0, 0, 0, 0x05, 0x02, 0x01, 0x03, 0x42, 0x00];

    [TestMethod]
    public void SimpleBind_AsCurlSendsIt_IsLoggedWithThePasswordMasked() =>
        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
            [.. CurlBindMasked, .. CurlUnbind],
            [.. CurlBind, .. CurlUnbind]);

    [TestMethod]
    public void SimpleBind_AfterAnotherMessageAndASecondBind_IsMaskedEveryTime() =>
        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
            [.. CurlBindMasked, .. CurlUnbind, .. CurlBindMasked],
            [.. CurlBind, .. CurlUnbind, .. CurlBind]);

    [TestMethod]
    public void SimpleBind_FollowedByControlsInItsMessage_KeepsThem()
    {
        byte[] controls = Value(0xA0, 0x30, 0x00);
        byte[] sent = Value(
            0x30,
            [.. Value(0x02, 1), .. Value(0x60, [.. Value(0x02, 3), .. Value(0x04), .. Value(0x80, "s3cret-pw"u8.ToArray())]), .. controls]);

        // The enclosing lengths stay as received (ADR-0182, amendment 5).
        byte[] logged = [.. sent[..^(11 + controls.Length)], .. Value(0x80, "****"u8.ToArray()), .. controls];

        CollectionAssert.AreEqual(logged, SocksTunnelRedactionTestKit.Logged(sent));
    }

    [TestMethod]
    public void SaslBind_IsLoggedWithItsWholeCredentialsFieldMasked()
    {
        var sent = BindWith(Value(0xA3, [.. Value(0x04, "PLAIN"u8.ToArray()), .. Value(0x04, "\0app\0s3cret-pw"u8.ToArray())]));
        byte[] logged = [.. sent[..15], 0xA3, 0x04, .. "****"u8];

        CollectionAssert.AreEqual(logged, SocksTunnelRedactionTestKit.Logged(sent));
    }

    [TestMethod]
    public void SimpleBind_WithAnEmptyPassword_IsLoggedAsReceived()
    {
        var sent = BindWith(0x80, 0x00);

        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(sent, sent);
    }

    // Each row is a whole message whose bind cannot be walked to its authentication.
    [TestMethod]
    [DataRow(new byte[] { 0x30, 5, 0x02, 1, 1, 0x60, 0 }, DisplayName = "empty operation")]
    [DataRow(new byte[] { 0x30, 7, 0x02, 1, 1, 0x60, 9, 0x02, 1 }, DisplayName = "operation longer than the message")]
    [DataRow(new byte[] { 0x30, 7, 0x02, 1, 1, 0x60, 2, 0x02, 9 }, DisplayName = "version longer than the message")]
    [DataRow(new byte[] { 0x30, 10, 0x02, 1, 1, 0x60, 5, 0x02, 1, 3, 0x04, 9 }, DisplayName = "name longer than the message")]
    [DataRow(new byte[] { 0x30, 10, 0x02, 1, 1, 0x60, 5, 0x02, 1, 3, 0x04, 0 }, DisplayName = "no authentication")]
    [DataRow(new byte[] { 0x30, 13, 0x02, 1, 1, 0x60, 8, 0x02, 1, 3, 0x04, 0, 0x80, 9, 112 }, DisplayName = "authentication cut short")]
    [DataRow(new byte[] { 0x30, 7, 0x02, 1, 1, 0x60, 2, 0x02, 0x80 }, DisplayName = "indefinite length")]
    public void Bind_ThatCannotBeWalked_HasEverythingAfterItsOperationTagMasked(byte[] message)
    {
        byte[] logged = [.. message[..6], .. "****"u8, .. CurlUnbind];

        CollectionAssert.AreEqual(logged, SocksTunnelRedactionTestKit.Logged([.. message, .. CurlUnbind]));
    }

    [TestMethod]
    public void Bind_EndingAtItsOperationTag_IsLoggedWithAMask()
    {
        byte[] message = [0x30, 4, 0x02, 1, 1, 0x60];

        CollectionAssert.AreEqual((byte[])[.. message, .. "****"u8], SocksTunnelRedactionTestKit.Logged(message));
    }

    [TestMethod]
    public void Bind_CutOffInsideThePassword_IsLoggedMasked() =>
        CollectionAssert.AreEqual(
            (byte[])[.. CurlBind[..10], .. "****"u8],
            SocksTunnelRedactionTestKit.Logged(CurlBind[..^2]));

    // Cut off before the operation's tag, nothing held is a password.
    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(7)]
    [DataRow(9)]
    public void Bind_CutOffBeforeItsOperation_IsLoggedAsReceived(int length) =>
        CollectionAssert.AreEqual(CurlBind[..length], SocksTunnelRedactionTestKit.Logged(CurlBind[..length]));

    // A message too short to name an operation is logged, and the next message is still framed.
    [TestMethod]
    [DataRow(new byte[] { 0x30, 0 }, DisplayName = "empty")]
    [DataRow(new byte[] { 0x30, 1, 0x02 }, DisplayName = "half a message identifier")]
    [DataRow(new byte[] { 0x30, 3, 0x02, 1, 1 }, DisplayName = "no operation")]
    [DataRow(new byte[] { 0x30, 3, 0x02, 9, 1 }, DisplayName = "identifier longer than the message")]
    public void MalformedTunnel_AMessageWithNoOperation_IsLoggedAndTheNextBindMasked(byte[] message) =>
        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit([.. message, .. CurlBindMasked], [.. message, .. CurlBind]);

    // Bytes that cannot be an LDAPMessage pass through from there on, never held.
    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x80 }, DisplayName = "indefinite message length")]
    [DataRow(new byte[] { 0x30, 0x85 }, DisplayName = "five length octets")]
    [DataRow(new byte[] { 0x30, 0x40, 0x02, 0x80 }, DisplayName = "indefinite identifier length")]
    [DataRow(new byte[] { 0x30, 0x40, 0x02, 0x30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, DisplayName = "identifier too long")]
    [DataRow(new byte[] { 0x30, 3, 0x02, 1, 1, 0x16, 0x03 }, DisplayName = "next bytes are no sequence")]
    public void MalformedTunnel_BytesThatAreNoMessage_AreLoggedAsReceivedAndNeverHeld(byte[] opening)
    {
        byte[] later = [.. CurlBind, .. new byte[SocksTunnelLdapBindRedactor.MaxHeldBytes + 1]];
        var redactor = SocksTunnelRedactionTestKit.GrantedSocks5Tunnel();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(opening, output);
        CollectionAssert.AreEqual(opening, output.WrittenSpan.ToArray());

        redactor.Redact(later, output);
        CollectionAssert.AreEqual((byte[])[.. opening, .. later], output.WrittenSpan.ToArray());

        redactor.Flush(output);
        CollectionAssert.AreEqual((byte[])[.. opening, .. later], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void MalformedTunnel_AMessageOtherThanABind_IsLoggedBeforeItIsWhole()
    {
        // A search request declaring 16 MiB.
        byte[] opening = [0x30, 0x84, 0x01, 0, 0, 0, 0x02, 0x01, 0x02, 0x63];
        byte[] later = new byte[SocksTunnelLdapBindRedactor.MaxHeldBytes + 1];
        var redactor = SocksTunnelRedactionTestKit.GrantedSocks5Tunnel();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(opening, output);
        redactor.Redact(later, output);

        CollectionAssert.AreEqual((byte[])[.. opening, .. later], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void MalformedTunnel_ABindOverTheHeldLimit_IsOneMaskAndNeverHeld()
    {
        // A bind whose message is 6 + 65,536 bytes, then an unbind.
        byte[] opening = [0x30, 0x84, 0, 0x01, 0, 0, 0x02, 0x01, 0x01, 0x60];
        byte[] rest = new byte[65536 - 4];
        var redactor = SocksTunnelRedactionTestKit.GrantedSocks5Tunnel();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact(opening, output);
        CollectionAssert.AreEqual("****"u8.ToArray(), output.WrittenSpan.ToArray());

        redactor.Redact(rest.AsSpan(0, 40000), output);
        redactor.Redact([.. rest.AsSpan(40000), .. CurlUnbind], output);
        redactor.Flush(output);

        CollectionAssert.AreEqual((byte[])[.. "****"u8, .. CurlUnbind], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void LdapRedactor_NullOutput_IsRefused()
    {
        var redactor = new SocksTunnelLdapBindRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([0x30], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    // One BER value with a short-form length.
    private static byte[] Value(byte tag, params byte[] value) => [tag, (byte)value.Length, .. value];

    private static byte[] BindWith(params byte[] authentication) =>
        Value(0x30, [.. Value(0x02, 1), .. Value(0x60, [.. Value(0x02, 3), .. Value(0x04, "app"u8.ToArray()), .. authentication])]);
}
