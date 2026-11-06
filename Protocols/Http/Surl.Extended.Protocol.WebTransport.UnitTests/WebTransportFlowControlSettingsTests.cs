namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The flow-control limits the server offers, and when flow control is on (ADR-0117 decision 4; WTH3 5.1, 5.5).</summary>
[TestClass]
public sealed class WebTransportFlowControlSettingsTests
{
    [TestMethod]
    public void ToHttp3Settings_Default_IsTheThreeInitialLimitsOfDecision4()
    {
        var settings = WebTransportFlowControlSettings.Default.ToHttp3Settings();

        CollectionAssert.AreEqual(
            new KeyValuePair<ulong, ulong>[] { new(0x2b64, 100), new(0x2b65, 100), new(0x2b61, 1048576) },
            settings);
    }

    [TestMethod]
    [DataRow(0x2b61UL, 1UL, true)]
    [DataRow(0x2b64UL, 1UL, true)]
    [DataRow(0x2b65UL, 1UL, true)]
    [DataRow(0x2b61UL, 0UL, false)]
    [DataRow(0x33UL, 1UL, false)]
    public void IsOnFor_ClientSettings_IsOnWhenAnyInitialLimitIsNonZero(ulong identifier, ulong value, bool expected)
    {
        var clientSettings = new Dictionary<ulong, ulong> { [identifier] = value };

        var isOn = WebTransportFlowControlSettings.IsOnFor(clientSettings);

        Assert.AreEqual(expected, isOn);
    }
}
