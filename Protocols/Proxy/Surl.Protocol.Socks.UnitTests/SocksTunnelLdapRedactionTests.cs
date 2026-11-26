namespace Surl.Protocol.Socks;

/// <summary>
/// LDAP bind secrets through a granted SOCKS tunnel, as the exchange log shows them (ADR-0182,
/// amendments 10 and 25). <c>Surl.LineProtocol</c>'s <c>TunnelLdapBindRedactorTests</c> cover every
/// form of the message.
/// </summary>
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
    public void SimpleBindAfterAProxyLine_IsLoggedWithThePasswordMasked()
    {
        // The line pinned upstream curl 8.21.0 sent through a SOCKS5 tunnel for
        // `--haproxy-protocol ... ldap://...`; its WinLDAP binds outside the tunnel (Fixtures/README.md).
        byte[] proxyLine = [.. "PROXY TCP4 127.0.0.1 127.0.0.1 52098 18781\r\n"u8];

        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
            [.. proxyLine, .. CurlBindMasked, .. CurlUnbind],
            [.. proxyLine, .. CurlBind, .. CurlUnbind]);
    }

    [TestMethod]
    public void Bind_CutOffInsideThePassword_IsLoggedMasked() =>
        CollectionAssert.AreEqual(
            (byte[])[.. CurlBind[..10], .. "****"u8],
            SocksTunnelRedactionTestKit.Logged(CurlBind[..^2]));
}
