using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Xmpp.UnitTests.XmppLoginScript;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// What the exchange log holds of an XMPP client's SASL PLAIN login (BL-1463; ADR-0182 decisions 1 and 6):
/// the text of <c>&lt;auth mechanism='PLAIN'&gt;</c> is <c>****</c>, everything else as received.
/// </summary>
[TestClass]
public sealed class XmppExchangeLogRedactionTests
{
    private const string Plain = "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>AHVzZXIAcGVuY2ls</auth>";
    private const string PlainLogged = "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>****</auth>";

    private static IReceivedBytesRedactor NewRedactor() => new XmppServer().CreateRedactor(new ExchangeRedactionContext(1, "xmpp"));

    private static string Logged(params string[] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.UTF8.GetBytes(chunk), output);
        }

        redactor.Flush(output);
        return Encoding.UTF8.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void PlainAuth_MasksTheBase64TextAndKeepsTheRest() =>
        Assert.AreEqual(ClientHeader + PlainLogged + Close, Logged(ClientHeader + Plain + Close));

    [TestMethod]
    public void PlainAuth_SplitAtEveryByte_IsLoggedTheSameAsWhole()
    {
        var wire = ClientHeader + Plain + Close;

        for (var split = 0; split <= wire.Length; split++)
        {
            Assert.AreEqual(ClientHeader + PlainLogged + Close, Logged(wire[..split], wire[split..]), $"split at {split}");
        }
    }

    [TestMethod]
    public void PlainAuth_OneByteAtATime_IsMasked() =>
        Assert.AreEqual(PlainLogged, Logged([.. Plain.Select(c => c.ToString())]));

    [TestMethod]
    public void PlainAuth_WithDoubleQuotesAndAPrefix_IsMasked() =>
        Assert.AreEqual(
            "<sasl:auth xmlns:sasl='urn:ietf:params:xml:ns:xmpp-sasl' mechanism = \"PLAIN\">****</sasl:auth>",
            Logged("<sasl:auth xmlns:sasl='urn:ietf:params:xml:ns:xmpp-sasl' mechanism = \"PLAIN\">AHVzZXIAcGVuY2ls</sasl:auth>"));

    [TestMethod]
    public void PlainAuth_WithACdataBody_IsMaskedWholeAndTheNextElementIsLogged() =>
        Assert.AreEqual(
            "<auth mechanism='PLAIN'>****</auth><iq id='1'/>",
            Logged("<auth mechanism='PLAIN'><![CDATA[AHVzZXIAcGVuY2ls]]></auth><iq id='1'/>"));

    [TestMethod]
    public void PlainAuth_WithAnAttributeHoldingABracket_IsMasked() =>
        Assert.AreEqual(
            "<auth note='a>b' mechanism='PLAIN'>****</auth>",
            Logged("<auth note='a>b' mechanism='PLAIN'>AHVzZXIAcGVuY2ls</auth>"));

    [TestMethod]
    public void PlainAuth_WithAnEmptyBody_LogsNoMask() =>
        Assert.AreEqual("<auth mechanism='PLAIN'></auth>", Logged("<auth mechanism='PLAIN'></auth>"));

    [TestMethod]
    public void PlainAuth_WithNoInitialResponse_MasksTheResponseThatAnswersTheChallenge() =>
        Assert.AreEqual(
            "<auth mechanism='PLAIN'/><response>****</response><response>kept</response>",
            Logged("<auth mechanism='PLAIN'/>", "<response>AHVzZXIAcGVuY2ls</response>", "<response>kept</response>"));

    [TestMethod]
    public void Response_WithNoPlainAuthBefore_IsLoggedAsReceived() =>
        Assert.AreEqual("<response>abc</response>", Logged("<response>abc</response>"));

    [TestMethod]
    public void Response_AfterAnotherAuthHasReplacedThePlainOne_IsLoggedAsReceived() =>
        Assert.AreEqual(
            "<auth mechanism='PLAIN'/><auth mechanism='SCRAM-SHA-1'>biws</auth><response>abc</response>",
            Logged("<auth mechanism='PLAIN'/><auth mechanism='SCRAM-SHA-1'>biws</auth><response>abc</response>"));

    [TestMethod]
    [DataRow("<auth mechanism='SCRAM-SHA-256'>biwsLHI9</auth>")]
    [DataRow("<auth>AHVzZXIAcGVuY2ls</auth>")]
    [DataRow("<auth mechanism>AHVzZXIAcGVuY2ls</auth>")]
    [DataRow("<auth mechanism='PLAINER'>x</auth>")]
    [DataRow("<message to='a@b'>PLAIN</message>")]
    [DataRow("<!-- note --><iq id='1'/>")]
    public void OtherElements_AreLoggedAsReceived(string wire) => Assert.AreEqual(wire, Logged(wire));

    [TestMethod]
    public void AuthWhoseMechanismHasNoEqualsSign_IsLoggedAsANonPlainElement() =>
        Assert.AreEqual(
            "<auth mechanism PLAIN>AHVzZXIAcGVuY2ls</auth>",
            Logged("<auth mechanism PLAIN>AHVzZXIAcGVuY2ls</auth>"));

    [TestMethod]
    public void AuthWhoseMechanismEndsTheTagText_IsLoggedAsANonPlainElement() =>
        Assert.AreEqual(
            "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism>AHVzZXIAcGVuY2ls</auth>",
            Logged("<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism>AHVzZXIAcGVuY2ls</auth>"));

    [TestMethod]
    public void UnfinishedTag_AtFlush_IsLoggedAsReceived() =>
        Assert.AreEqual("<auth mechanism='PLA", Logged("<auth mechanism='PLA"));

    [TestMethod]
    public void UnfinishedPlainText_AtFlush_IsMasked() =>
        Assert.AreEqual("<auth mechanism='PLAIN'>****", Logged("<auth mechanism='PLAIN'>AHVz"));

    [TestMethod]
    public void TagPastTheHeldLimit_IsLoggedAsReceivedAndReadNoFurther()
    {
        var huge = "<x " + new string('a', XmppSaslPlainRedactor.MaxHeldBytes) + ">";

        Assert.AreEqual(huge + PlainLogged, Logged(huge[..100], huge[100..] + Plain));
    }

    [TestMethod]
    public void Redactor_GuardsItsArgumentsAndIgnoresWhatTheServerSent()
    {
        var redactor = NewRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
        redactor.ObserveSent("<success/>"u8);
    }

    [TestMethod]
    public async Task Server_AfterAPlainLogin_StillReceivesTheRawBytesAndAnswersAsBefore()
    {
        var (answers, _) = await ServeAsync(
            Options(PlainPolicy()), TestContext.CancellationToken,
            ClientHeader, Auth("PLAIN", PlainUserPencil), ClientHeader, Close);

        Assert.AreEqual(
            ResponseHeader(1) + MechanismFeatures("PLAIN") + Success + ResponseHeader(2) + BindFeatures + Close,
            answers);
    }

    public TestContext TestContext { get; set; } = null!;
}
