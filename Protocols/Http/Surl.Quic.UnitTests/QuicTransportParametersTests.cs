namespace Surl.Quic;

/// <summary>
/// Reads and writes transport parameters (RFC 9000 section 18, RFC 9368 section 3). The encoded
/// parameters are written from section 18.2's table; the recorded curl parameters are ADR-0082's
/// measured ones.
/// </summary>
[TestClass]
public sealed class QuicTransportParametersTests
{
    [TestMethod]
    public void Read_ParametersCurlSends_GivesEachValue()
    {
        var encoded = Convert.FromHexString("0f0401020304" + UnitTestClientHelloEdits.RecordedParametersAfterSourceConnectionId);

        var parameters = QuicTransportParameters.Read(encoded);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, parameters.InitialSourceConnectionId!.Value.ToArray());
        Assert.AreEqual((ulong)32768, parameters.InitialMaxStreamDataBidiLocal);
        Assert.AreEqual((ulong)32768, parameters.InitialMaxStreamDataBidiRemote);
        Assert.AreEqual((ulong)1048576000, parameters.InitialMaxStreamDataUni);
        Assert.AreEqual((ulong)1048576000, parameters.InitialMaxData);
        Assert.AreEqual((ulong)262144, parameters.InitialMaxStreamsBidi);
        Assert.AreEqual((ulong)262144, parameters.InitialMaxStreamsUni);
        Assert.AreEqual(QuicVersions.Version1, parameters.VersionInformation!.ChosenVersion);
        CollectionAssert.AreEqual(new[] { QuicVersions.Version1 }, parameters.VersionInformation.AvailableVersions.ToArray());
        Assert.IsNull(parameters.MaxIdleTimeout);
        Assert.IsFalse(parameters.DisableActiveMigration);
    }

    [TestMethod]
    public void ToBytes_EveryParameter_ReadsBackTheSame()
    {
        var parameters = new QuicTransportParameters
        {
            OriginalDestinationConnectionId = new byte[] { 1 },
            MaxIdleTimeout = 30000,
            StatelessResetToken = new byte[16],
            MaxUdpPayloadSize = 1200,
            InitialMaxData = 4,
            InitialMaxStreamDataBidiLocal = 5,
            InitialMaxStreamDataBidiRemote = 6,
            InitialMaxStreamDataUni = 7,
            InitialMaxStreamsBidi = 8,
            InitialMaxStreamsUni = 9,
            AckDelayExponent = 20,
            MaxAckDelay = 16383,
            DisableActiveMigration = true,
            PreferredAddress = new byte[41],
            ActiveConnectionIdLimit = 2,
            InitialSourceConnectionId = new byte[20],
            RetrySourceConnectionId = ReadOnlyMemory<byte>.Empty,
            VersionInformation = new QuicVersionInformation(1, [1, 0x6b3343cf]),
        };

        var read = QuicTransportParameters.Read(parameters.ToBytes());

        Assert.AreEqual("01", Convert.ToHexString(read.OriginalDestinationConnectionId!.Value.Span));
        Assert.AreEqual((ulong)30000, read.MaxIdleTimeout);
        Assert.AreEqual(16, read.StatelessResetToken!.Value.Length);
        Assert.AreEqual((ulong)1200, read.MaxUdpPayloadSize);
        Assert.AreEqual((ulong)4, read.InitialMaxData);
        Assert.AreEqual((ulong)5, read.InitialMaxStreamDataBidiLocal);
        Assert.AreEqual((ulong)6, read.InitialMaxStreamDataBidiRemote);
        Assert.AreEqual((ulong)7, read.InitialMaxStreamDataUni);
        Assert.AreEqual((ulong)8, read.InitialMaxStreamsBidi);
        Assert.AreEqual((ulong)9, read.InitialMaxStreamsUni);
        Assert.AreEqual((ulong)20, read.AckDelayExponent);
        Assert.AreEqual((ulong)16383, read.MaxAckDelay);
        Assert.IsTrue(read.DisableActiveMigration);
        Assert.AreEqual(41, read.PreferredAddress!.Value.Length);
        Assert.AreEqual((ulong)2, read.ActiveConnectionIdLimit);
        Assert.AreEqual(20, read.InitialSourceConnectionId!.Value.Length);
        Assert.IsTrue(read.RetrySourceConnectionId!.Value.IsEmpty);
        CollectionAssert.AreEqual(new uint[] { 1, 0x6b3343cf }, read.VersionInformation!.AvailableVersions.ToArray());
    }

    [TestMethod]
    public void ToBytes_NothingPresent_IsEmpty()
    {
        Assert.IsEmpty(new QuicTransportParameters().ToBytes());
    }

    [TestMethod]
    public void ToBytes_ParametersPresent_AreWrittenInIdOrderAsSection18Says()
    {
        var parameters = new QuicTransportParameters { InitialSourceConnectionId = new byte[] { 0xaa }, MaxIdleTimeout = 100, DisableActiveMigration = true };

        Assert.AreEqual("010240640C000F01AA", Convert.ToHexString(parameters.ToBytes()));
    }

    [TestMethod]
    public void ToBytes_ConnectionIdOver20Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicTransportParameters { InitialSourceConnectionId = new byte[21] }.ToBytes());
    }

    [TestMethod]
    public void Read_UnknownParameter_IsSkipped()
    {
        var parameters = QuicTransportParameters.Read(Convert.FromHexString("1b03aabbcc" + "0401" + "05"));

        Assert.AreEqual((ulong)5, parameters.InitialMaxData);
    }

    [TestMethod]
    [DataRow("a parameter twice", "04010504 0105")]
    [DataRow("a value that ends early", "0405 01")]
    [DataRow("an integer with bytes after it", "04020500")]
    [DataRow("an empty integer", "0400")]
    [DataRow("max_udp_payload_size below 1200", "03024000")]
    [DataRow("initial_max_streams_bidi above 2^60", "0808d000000000000001")]
    [DataRow("initial_max_streams_uni above 2^60", "0908d000000000000001")]
    [DataRow("ack_delay_exponent above 20", "0a0115")]
    [DataRow("max_ack_delay of 2^14", "0b0480004000")]
    [DataRow("active_connection_id_limit below 2", "0e0101")]
    [DataRow("disable_active_migration with a value", "0c0100")]
    [DataRow("a 15-byte stateless_reset_token", "020f000000000000000000000000000000")]
    [DataRow("a 17-byte stateless_reset_token", "02110000000000000000000000000000000000")]
    [DataRow("a 40-byte preferred_address", "0d2800000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("a 21-byte connection ID", "0f15000000000000000000000000000000000000000000")]
    [DataRow("a 21-byte original_destination_connection_id", "0015000000000000000000000000000000000000000000")]
    [DataRow("a 21-byte retry_source_connection_id", "1015000000000000000000000000000000000000000000")]
    [DataRow("version_information of 3 bytes", "1103000000")]
    [DataRow("version_information of 6 bytes", "1106000000010000")]
    [DataRow("an empty version_information", "1100")]
    [DataRow("reset_stream_at with a value", "1d0100")]
    public void Read_MalformedParameter_IsTransportParameterError(string description, string hex)
    {
        var encoded = Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicTransportParameters.Read(encoded), description);

        Assert.AreEqual(QuicTransportErrorCode.TransportParameterError, exception.ErrorCode, description);
    }

    [TestMethod]
    public void Read_MaxDatagramFrameSize_GivesItsValue()
    {
        var parameters = QuicTransportParameters.Read(Convert.FromHexString("200244b0"));

        Assert.AreEqual(1200UL, parameters.MaxDatagramFrameSize);
    }

    [TestMethod]
    public void ToBytes_MaxDatagramFrameSize_WritesParameter0x20AfterVersionInformation()
    {
        var parameters = new QuicTransportParameters { MaxDatagramFrameSize = 65535, VersionInformation = new QuicVersionInformation(QuicVersions.Version1, []) };

        var encoded = parameters.ToBytes();

        Assert.AreEqual("11040000000120048000FFFF", Convert.ToHexString(encoded));
    }

    [TestMethod]
    public void Read_MaxDatagramFrameSizeNotOneVarint_IsTransportParameterError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicTransportParameters.Read(Convert.FromHexString("200244")));

        Assert.AreEqual(QuicTransportErrorCode.TransportParameterError, exception.ErrorCode);
    }

    [TestMethod]
    public void Read_ResetStreamAt_IsPresent()
    {
        Assert.IsTrue(QuicTransportParameters.Read(Convert.FromHexString("1d00")).ResetStreamAt);
        Assert.IsFalse(QuicTransportParameters.Read(ReadOnlyMemory<byte>.Empty).ResetStreamAt);
    }

    [TestMethod]
    public void ToBytes_ResetStreamAt_WritesTheEmptyParameter0x1dBeforeMaxDatagramFrameSize()
    {
        var parameters = new QuicTransportParameters { ResetStreamAt = true, MaxDatagramFrameSize = 1, VersionInformation = new QuicVersionInformation(QuicVersions.Version1, []) };

        Assert.AreEqual("1104000000011D00200101", Convert.ToHexString(parameters.ToBytes()));
    }
}
