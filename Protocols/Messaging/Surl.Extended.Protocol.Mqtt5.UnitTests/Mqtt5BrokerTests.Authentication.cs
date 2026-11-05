using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Enhanced authentication and re-authentication with the AUTH packet (ADR-0093 decision 2; MQTT
/// 5.0 3.15 and 4.12). The SCRAM messages are RFC 7677's shapes; the policy is scripted, so the
/// tests pin the MQTT framing of each step.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    private const string ScramSha256 = "SCRAM-SHA-256";
    private const string ClientFirst = "n,,n=user,r=rOprNGfwEbeRWgbNEkqO";
    private const string ServerFirst = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
    private const string ClientFinal = "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
    private const string ServerFinal = "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=";

    [TestMethod]
    public async Task ServeAsync_ScramSha256Authentication_CompletesWithContinueAuthenticationSteps()
    {
        var policy = ScramPolicy(UnitTestSaslAuthenticationPolicy.Accepted("user"));

        var connection = await ServeAsync(
            Broker(NewState(), new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials), policy),
            Context(new ManualTimeProvider(), TestContext.CancellationToken),
            ConnectWithMethod(ScramSha256, ClientFirst),
            Auth(0x18, ScramSha256, ClientFinal),
            DisconnectNormally);

        CollectionAssert.AreEqual(Concat(Auth(0x18, ScramSha256, ServerFirst), ConnackWithAuthentication(ScramSha256, ServerFinal)), connection.WrittenBytes);
        Assert.AreEqual(ScramSha256, policy.Starts.Single().Mechanism);
        Assert.AreEqual(ClientFirst, Encoding.ASCII.GetString(policy.Starts.Single().InitialResponse!.Value.Span));
        CollectionAssert.AreEqual(new[] { ClientFinal, string.Empty }, policy.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_MethodAcceptedUncheckedAtOnce_AnswersConnackWithTheMethodAlone()
    {
        var policy = new UnitTestSaslAuthenticationPolicy("SCRAM-SHA-1")
        {
            ScriptedSteps = [new(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null)],
        };

        var connection = await ServeAsync(
            Broker(NewState(), saslPolicy: policy),
            Context(new ManualTimeProvider(), TestContext.CancellationToken),
            Encode(new Mqtt5ConnectPacket(true, 30, [Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AuthenticationMethod, "SCRAM-SHA-1")], "c1", null, null, null)),
            DisconnectNormally);

        CollectionAssert.AreEqual(ConnackWithAuthentication("SCRAM-SHA-1", null), connection.WrittenBytes);
        Assert.IsEmpty(policy.Starts.Single().InitialResponse!.Value.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Reauthentication_ContinuesWithContinueAuthenticationAndEndsWithAuthSuccess()
    {
        var policy = ScramPolicy(
            UnitTestSaslAuthenticationPolicy.Accepted("user"),
            UnitTestSaslAuthenticationPolicy.Challenge(ServerFirst),
            UnitTestSaslAuthenticationPolicy.Challenge(ServerFinal),
            UnitTestSaslAuthenticationPolicy.Accepted("user"));

        var connection = await ServeAsync(
            Broker(NewState(), saslPolicy: policy),
            Context(new ManualTimeProvider(), TestContext.CancellationToken),
            ConnectWithMethod(ScramSha256, ClientFirst),
            Auth(0x18, ScramSha256, ClientFinal),
            Auth(0x19, ScramSha256, ClientFirst),
            Auth(0x18, ScramSha256, ClientFinal),
            PingRequest,
            DisconnectNormally);

        CollectionAssert.AreEqual(
            Concat(
                Auth(0x18, ScramSha256, ServerFirst),
                ConnackWithAuthentication(ScramSha256, ServerFinal),
                Auth(0x18, ScramSha256, ServerFirst),
                Auth(0x00, ScramSha256, ServerFinal),
                [0xD0, 0x00]),
            connection.WrittenBytes);
        Assert.HasCount(2, policy.Starts);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "The exchange refuses the client")]
    [DataRow(true, DisplayName = "The exchange authenticates another account")]
    public async Task ServeAsync_ReauthenticationFails_DisconnectsWithNotAuthorized(bool anotherAccount)
    {
        var policy = ScramPolicy(
            UnitTestSaslAuthenticationPolicy.Accepted("user"),
            anotherAccount ? UnitTestSaslAuthenticationPolicy.Accepted("other") : UnitTestSaslAuthenticationPolicy.Refused());
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(
            Broker(NewState(), saslPolicy: policy),
            Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log),
            ConnectWithMethod(ScramSha256, ClientFirst),
            Auth(0x18, ScramSha256, ClientFinal),
            Auth(0x19, ScramSha256, ClientFirst));

        CollectionAssert.AreEqual(
            Concat(Auth(0x18, ScramSha256, ServerFirst), ConnackWithAuthentication(ScramSha256, ServerFinal), Disconnect(0x87)),
            connection.WrittenBytes);
        Assert.Contains("Re-authentication failed ([MQTT-4.12.1-2]); sent DISCONNECT 0x87 and closed.", log.Notes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xF0, 0x02, 0x18, 0x00 }, DisplayName = "AUTH 0x18 with no re-authentication under way")]
    [DataRow(new byte[] { 0xF0, 0x02, 0x00, 0x00 }, DisplayName = "AUTH 0x00")]
    [DataRow(new byte[] { 0xF0, 0x02, 0x19, 0x00 }, DisplayName = "AUTH 0x19 naming no method")]
    [DataRow(new byte[] { 0xF0, 0x10, 0x19, 0x0E, 0x15, 0x00, 0x0B, 0x53, 0x43, 0x52, 0x41, 0x4D, 0x2D, 0x53, 0x48, 0x41, 0x2D, 0x31 }, DisplayName = "AUTH 0x19 naming another method")]
    public async Task ServeAsync_AuthThatDoesNotContinueTheAuthentication_DisconnectsWithProtocolError(byte[] authentication)
    {
        var policy = ScramPolicy(UnitTestSaslAuthenticationPolicy.Accepted("user"));

        var connection = await ServeAsync(
            Broker(NewState(), saslPolicy: policy),
            Context(new ManualTimeProvider(), TestContext.CancellationToken),
            ConnectWithMethod(ScramSha256, ClientFirst),
            Auth(0x18, ScramSha256, ClientFinal),
            authentication);

        CollectionAssert.AreEqual(
            Concat(Auth(0x18, ScramSha256, ServerFirst), ConnackWithAuthentication(ScramSha256, ServerFinal), Disconnect(0x82)),
            connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow("PLAIN", DisplayName = "A method the policy offers that the broker does not serve")]
    [DataRow("SCRAM-SHA-1", DisplayName = "A SCRAM method the policy does not offer")]
    [DataRow("scram-sha-256", DisplayName = "A method named in another case")]
    public async Task ServeAsync_UnknownAuthenticationMethod_AnswersBadAuthenticationMethod(string method)
    {
        var policy = new UnitTestSaslAuthenticationPolicy(ScramSha256, "PLAIN");

        var connection = await ServeAsync(Broker(NewState(), saslPolicy: policy), Context(new ManualTimeProvider(), TestContext.CancellationToken), ConnectWithMethod(method, ClientFirst));

        CollectionAssert.AreEqual(ConnackRefused(0x8C), connection.WrittenBytes);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_EnhancedAuthenticationRefused_AnswersNotAuthorized()
    {
        var policy = new UnitTestSaslAuthenticationPolicy(ScramSha256)
        {
            ScriptedSteps = [UnitTestSaslAuthenticationPolicy.Challenge(ServerFirst), UnitTestSaslAuthenticationPolicy.Refused()],
        };
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(
            Broker(NewState(), saslPolicy: policy),
            Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log),
            ConnectWithMethod(ScramSha256, ClientFirst),
            Auth(0x18, ScramSha256, ClientFinal));

        CollectionAssert.AreEqual(Concat(Auth(0x18, ScramSha256, ServerFirst), ConnackRefused(0x87)), connection.WrittenBytes);
        Assert.Contains("SCRAM: invalid proof", log.Notes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xC0, 0x00 }, (byte)0x82, DisplayName = "PINGREQ")]
    [DataRow(new byte[] { 0xF0, 0x02, 0x19, 0x00 }, (byte)0x82, DisplayName = "AUTH 0x19")]
    [DataRow(new byte[] { 0xF0, 0x02, 0x18, 0x00 }, (byte)0x82, DisplayName = "AUTH 0x18 naming no method")]
    [DataRow(new byte[] { 0xF0, 0x10, 0x18, 0x0E, 0x15, 0x00, 0x0B, 0x53, 0x43, 0x52, 0x41, 0x4D, 0x2D, 0x53, 0x48, 0x41, 0x2D, 0x31 }, (byte)0x82, DisplayName = "AUTH 0x18 naming another method")]
    [DataRow(new byte[] { 0xF1, 0x00 }, (byte)0x81, DisplayName = "AUTH with reserved flags set")]
    public async Task ServeAsync_WrongPacketDuringTheAuthentication_AnswersItsReasonCode(byte[] packet, byte reasonCode)
    {
        var policy = ScramPolicy();

        var connection = await ServeAsync(Broker(NewState(), saslPolicy: policy), Context(new ManualTimeProvider(), TestContext.CancellationToken), ConnectWithMethod(ScramSha256, ClientFirst), packet);

        CollectionAssert.AreEqual(Concat(Auth(0x18, ScramSha256, ServerFirst), ConnackRefused(reasonCode)), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xE0, 0x00 }, DisplayName = "DISCONNECT")]
    [DataRow(new byte[0], DisplayName = "The client closes")]
    public async Task ServeAsync_ClientEndsTheAuthentication_ClosesWithNoConnack(byte[] last)
    {
        var policy = ScramPolicy();
        byte[][] packets = last.Length == 0 ? [ConnectWithMethod(ScramSha256, ClientFirst)] : [ConnectWithMethod(ScramSha256, ClientFirst), last];

        var connection = await ServeAsync(Broker(NewState(), saslPolicy: policy), Context(new ManualTimeProvider(), TestContext.CancellationToken), packets);

        CollectionAssert.AreEqual(Auth(0x18, ScramSha256, ServerFirst), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_AuthenticationPastTheHeadTimeout_ClosesWithNoConnack()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = ConnectionSending(staysOpen: true, ConnectWithMethod(ScramSha256, ClientFirst));

        var serving = Broker(NewState(), saslPolicy: ScramPolicy()).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        CollectionAssert.AreEqual(Auth(0x18, ScramSha256, ServerFirst), connection.WrittenBytes);
        Assert.Contains("An AUTH of the CONNECT's authentication was not complete within the head timeout; closed with no reply.", log.Notes);
    }

    // A policy offering SCRAM-SHA-256 whose first exchange answers the client-first with the
    // server-first and the client-final with the server-final, then takes the steps given.
    private static UnitTestSaslAuthenticationPolicy ScramPolicy(params SaslLoginStep[] laterSteps) => new(ScramSha256)
    {
        ScriptedSteps = [UnitTestSaslAuthenticationPolicy.Challenge(ServerFirst), UnitTestSaslAuthenticationPolicy.Challenge(ServerFinal), .. laterSteps],
    };

    private static byte[] Encode(Mqtt5Packet packet) => Mqtt5PacketEncoder.Encode(packet);

    private static byte[] ConnectWithMethod(string method, string data) => Connect(properties:
    [
        Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AuthenticationMethod, method),
        Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.AuthenticationData, Encoding.ASCII.GetBytes(data)),
    ]);

    // The Authentication Method (0x15) and, unless null, Authentication Data (0x16) properties.
    private static byte[] AuthenticationProperties(string method, string? data)
    {
        var methodBytes = Encoding.ASCII.GetBytes(method);
        byte[] properties = [0x15, 0x00, (byte)methodBytes.Length, .. methodBytes];
        if (data is null)
        {
            return properties;
        }

        var dataBytes = Encoding.ASCII.GetBytes(data);
        return [.. properties, 0x16, 0x00, (byte)dataBytes.Length, .. dataBytes];
    }

    // AUTH (3.15): fixed header 0xF0, Reason Code, Property Length, then the properties.
    private static byte[] Auth(byte reasonCode, string method, string data)
    {
        var properties = AuthenticationProperties(method, data);
        return [0xF0, (byte)(2 + properties.Length), reasonCode, (byte)properties.Length, .. properties];
    }

    // CONNACK Success with the default properties of ConnackSuccess, then the method and its data.
    private static byte[] ConnackWithAuthentication(string method, string? data)
    {
        byte[] properties = [.. ConnackSuccess[5..], .. AuthenticationProperties(method, data)];
        return [0x20, (byte)(3 + properties.Length), 0x00, 0x00, (byte)properties.Length, .. properties];
    }

    private static byte[] Concat(params byte[][] packets) => [.. packets.SelectMany(packet => packet)];
}
