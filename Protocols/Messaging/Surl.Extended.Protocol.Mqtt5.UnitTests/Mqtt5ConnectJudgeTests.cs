namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The value checks a decoded <c>CONNECT</c> passes before its login (ADR-0093 decision 1).
/// </summary>
[TestClass]
public sealed class Mqtt5ConnectJudgeTests
{
    [TestMethod]
    public void FaultIn_PropertiesWithinTheirRanges_IsNull()
    {
        var connect = ConnectWith(
            [
                Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, 1),
                Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MaximumPacketSize, 1),
                Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.RequestResponseInformation, 1),
                Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.RequestProblemInformation, 0),
                Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SessionExpiryInterval, 0),
            ],
            new Mqtt5Will(0, false, [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.PayloadFormatIndicator, 1)], "w", []));

        Assert.IsNull(Mqtt5ConnectJudge.FaultIn(connect));
    }

    [TestMethod]
    [DataRow((byte)Mqtt5PropertyIdentifier.ReceiveMaximum, 0u, "3.1.2.11.3")]
    [DataRow((byte)Mqtt5PropertyIdentifier.MaximumPacketSize, 0u, "3.1.2.11.4")]
    [DataRow((byte)Mqtt5PropertyIdentifier.RequestResponseInformation, 2u, "3.1.2.11.6")]
    [DataRow((byte)Mqtt5PropertyIdentifier.RequestProblemInformation, 2u, "3.1.2.11.7")]
    public void FaultIn_ForbiddenPropertyValue_IsAProtocolError(byte identifier, uint value, string section)
    {
        var fault = Mqtt5ConnectJudge.FaultIn(ConnectWith([Mqtt5Property.FromInteger((Mqtt5PropertyIdentifier)identifier, value)]));

        Assert.AreEqual(Mqtt5PacketFault.ProtocolError(section), fault);
    }

    [TestMethod]
    public void FaultIn_WillPayloadFormatIndicatorAbove1_IsAProtocolError()
    {
        var will = new Mqtt5Will(0, false, [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.PayloadFormatIndicator, 2)], "w", []);

        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("3.1.3.2.3"), Mqtt5ConnectJudge.FaultIn(ConnectWith([], will)));
    }

    [TestMethod]
    public void FaultIn_AuthenticationMethod_IsBadAuthenticationMethod()
    {
        var connect = ConnectWith([Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AuthenticationMethod, "SCRAM-SHA-256")]);

        Assert.AreEqual(Mqtt5ReasonCode.BadAuthenticationMethod, Mqtt5ConnectJudge.FaultIn(connect)!.ReasonCode);
    }

    [TestMethod]
    public void FaultIn_AuthenticationDataWithoutAMethod_IsAProtocolError()
    {
        var connect = ConnectWith([Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.AuthenticationData, [1])]);

        Assert.AreEqual(Mqtt5PacketFault.ProtocolError("3.1.2.11.10"), Mqtt5ConnectJudge.FaultIn(connect));
    }

    private static Mqtt5ConnectPacket ConnectWith(IReadOnlyList<Mqtt5Property> properties, Mqtt5Will? will = null) =>
        new(true, 30, properties, "c", will, null, null);
}
