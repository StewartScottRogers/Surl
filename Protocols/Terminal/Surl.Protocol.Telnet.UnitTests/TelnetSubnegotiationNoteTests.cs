namespace Surl.Protocol.Telnet;

[TestClass]
public sealed class TelnetSubnegotiationNoteTests
{
    private const byte TerminalType = 24;
    private const byte WindowSize = 31;
    private const byte DisplayLocation = 35;
    private const byte NewEnvironment = 39;

    [TestMethod]
    public void Describe_EnvironmentWithUserVariableUndefinedVariableAndEscape_ListsEachVariable()
    {
        byte[] payload = [0, 3, .. "HOME"u8, 1, .. "/h"u8, 0, .. "EMPTY"u8, 1, 0, .. "UNSET"u8, 0, .. "A"u8, 2, 1, .. "B"u8, 1, .. "c"u8];

        var note = TelnetSubnegotiationNote.Describe(NewEnvironment, payload);

        Assert.AreEqual("The client's environment (NEW-ENVIRON) is HOME=/h, EMPTY=, UNSET (undefined), A\u0001B=c.", note);
    }

    [TestMethod]
    public void Describe_EnvironmentInfoWithBytesBeforeTheFirstVariable_IgnoresThem()
    {
        byte[] payload = [2, .. "junk"u8, 1, .. "x"u8, 0, .. "USER"u8, 1, .. "bob"u8];

        var note = TelnetSubnegotiationNote.Describe(NewEnvironment, payload);

        Assert.AreEqual("The client's environment (NEW-ENVIRON) is USER=bob.", note);
    }

    [TestMethod]
    public void Describe_EnvironmentEndingInEscape_KeepsTheEscapeByte()
    {
        byte[] payload = [0, 0, .. "A"u8, 2];

        var note = TelnetSubnegotiationNote.Describe(NewEnvironment, payload);

        Assert.AreEqual("The client's environment (NEW-ENVIRON) is A\u0002 (undefined).", note);
    }

    [TestMethod]
    public void Describe_EmptyEnvironment_SaysItIsEmpty()
    {
        var note = TelnetSubnegotiationNote.Describe(NewEnvironment, [0]);

        Assert.AreEqual("The client's environment (NEW-ENVIRON) is empty.", note);
    }

    [TestMethod]
    public void Describe_DisplayLocation_ReportsIt()
    {
        var note = TelnetSubnegotiationNote.Describe(DisplayLocation, [0, .. "host:0"u8]);

        Assert.AreEqual("The client's X display location (X-DISPLAY-LOCATION) is host:0.", note);
    }

    [TestMethod]
    [DataRow(TerminalType, new byte[] { 1 })]
    [DataRow(TerminalType, new byte[0])]
    [DataRow(DisplayLocation, new byte[] { 1 })]
    [DataRow(NewEnvironment, new byte[] { 1 })]
    [DataRow(WindowSize, new byte[] { 0, 80, 0 })]
    [DataRow((byte)99, new byte[] { 0 })]
    public void Describe_SubnegotiationSurlCannotRead_SaysItWasIgnored(byte option, byte[] payload)
    {
        var note = TelnetSubnegotiationNote.Describe(option, payload);

        Assert.AreEqual($"The client sent a subnegotiation for option {option} that surl cannot read; ignored.", note);
    }
}
