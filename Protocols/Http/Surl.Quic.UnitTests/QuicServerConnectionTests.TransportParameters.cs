using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>The transport parameters a connection sends are its caller's options (ADR-0086 decision 4).</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_OptionsNameOtherTransportParameters_SendsTheOptionsValues()
    {
        using var key = RSA.Create(2048);
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = [UnitTestCertificates.SelfSigned(key)],
            CertificateKey = key,
            ApplicationProtocols = ["doq"],
            IdleTimeout = TimeSpan.FromSeconds(7),
            InitialMaxData = 5000,
            InitialMaxStreamDataBidiRemote = 4000,
            InitialMaxStreamDataUni = 0,
            InitialMaxStreamsBidi = 9,
            InitialMaxStreamsUni = 0,
        };
        using var exchange = new UnitTestExchange(options, editClientHello: UnitTestClientHelloEdits.Replace("0268330568332d3239", "03646f710468712d69"));

        exchange.SendRecordedInitials();
        var parameters = QuicTransportParameters.Read(exchange.Client.Tls.ServerTransportParameters());

        Assert.AreEqual(7000UL, parameters.MaxIdleTimeout);
        Assert.AreEqual(5000UL, parameters.InitialMaxData);
        Assert.AreEqual(4000UL, parameters.InitialMaxStreamDataBidiRemote);
        Assert.AreEqual(0UL, parameters.InitialMaxStreamDataUni);
        Assert.AreEqual(9UL, parameters.InitialMaxStreamsBidi);
        Assert.AreEqual(0UL, parameters.InitialMaxStreamsUni);
        Assert.AreEqual("doq", exchange.Client.Tls.ApplicationProtocol);
    }

    /// <summary>
    /// ADR-0082 decision 4's parameters, written by hand from RFC 9000 sections 16 and 18.2:
    /// max_idle_timeout 30000, initial_max_data 1048576, initial_max_stream_data_bidi_remote
    /// 262144, initial_max_stream_data_uni 65536, initial_max_streams_bidi 100,
    /// initial_max_streams_uni 3, disable_active_migration, and version_information naming
    /// version 1 alone, between the two connection IDs.
    /// </summary>
    [TestMethod]
    public void ReceiveDatagram_Adr0082Options_SendExactlyTheTransportParameterBytesTheyAlwaysDid()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        exchange.SendRecordedInitials();

        var original = exchange.Client.OriginalDestinationConnectionId;
        var expected = Convert.FromHexString(
            "00" + original.Length.ToString("x2") + Convert.ToHexString(original)
            + "010480007530" + "040480100000" + "060480040000" + "070480010000" + "08024064" + "090103" + "0c00"
            + "0f08" + Convert.ToHexString(exchange.Client.ServerConnectionId!)
            + "11080000000100000001");
        CollectionAssert.AreEqual(expected, exchange.Client.Tls.ServerTransportParameters());
    }
}
