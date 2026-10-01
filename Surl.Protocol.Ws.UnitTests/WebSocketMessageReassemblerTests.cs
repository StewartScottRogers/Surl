namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WebSocketMessageReassemblerTests
{
    [TestMethod]
    public void Accept_Rfc6455FragmentedText_JoinsHelloAcrossBothFrames()
    {
        var reassembler = new WebSocketMessageReassembler(0);

        var first = reassembler.Accept(Frame(fin: false, WebSocketOpcode.Text, "Hel"u8.ToArray()));
        var second = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Continuation, "lo"u8.ToArray()));

        Assert.AreEqual(WebSocketReassemblyOutcome.FragmentHeld, first.Outcome);
        Assert.IsNull(first.Message);
        Assert.AreEqual(WebSocketReassemblyOutcome.MessageComplete, second.Outcome);
        Assert.AreEqual(WebSocketOpcode.Text, second.Message!.Opcode);
        CollectionAssert.AreEqual(Rfc6455Examples.Hello, second.Message.Payload);
    }

    [TestMethod]
    public void Accept_SingleFinishedBinaryFrame_IsACompleteMessage()
    {
        var step = new WebSocketMessageReassembler(0).Accept(Frame(fin: true, WebSocketOpcode.Binary, [0xFF, 0xFE]));

        Assert.AreEqual(WebSocketReassemblyOutcome.MessageComplete, step.Outcome);
        Assert.AreEqual(WebSocketOpcode.Binary, step.Message!.Opcode);
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFE }, step.Message.Payload);
    }

    [TestMethod]
    public void Accept_PingBetweenFragments_PassesThroughAndTheMessageStillCompletes()
    {
        var reassembler = new WebSocketMessageReassembler(0);
        var ping = Frame(fin: true, WebSocketOpcode.Ping, Rfc6455Examples.Hello);

        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Text, "Hel"u8.ToArray()));
        var control = reassembler.Accept(ping);
        var last = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Continuation, "lo"u8.ToArray()));

        Assert.AreEqual(WebSocketReassemblyOutcome.ControlFrame, control.Outcome);
        Assert.AreSame(ping, control.ControlFrame);
        Assert.IsNull(control.Message);
        CollectionAssert.AreEqual(Rfc6455Examples.Hello, last.Message!.Payload);
    }

    [TestMethod]
    public void Accept_ContinuationWithNoMessageStarted_IsContinuationWithoutMessage()
    {
        var step = new WebSocketMessageReassembler(0).Accept(Frame(fin: true, WebSocketOpcode.Continuation, []));

        Assert.AreEqual(WebSocketReassemblyOutcome.ContinuationWithoutMessage, step.Outcome);
    }

    [TestMethod]
    public void Accept_DataFrameInsideAnUnfinishedMessage_IsDataFrameInsideMessage()
    {
        var reassembler = new WebSocketMessageReassembler(0);
        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Binary, [1]));

        var step = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Text, [2]));

        Assert.AreEqual(WebSocketReassemblyOutcome.DataFrameInsideMessage, step.Outcome);
    }

    [TestMethod]
    public void Accept_AfterAMessageCompletes_TheNextStartsEmpty()
    {
        var reassembler = new WebSocketMessageReassembler(0);
        reassembler.Accept(Frame(fin: true, WebSocketOpcode.Binary, [1, 2]));

        var step = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Binary, [3]));

        CollectionAssert.AreEqual(new byte[] { 3 }, step.Message!.Payload);
    }

    [TestMethod]
    public void Accept_JoinedPayloadOverTheMaximum_IsMessageTooLarge()
    {
        var reassembler = new WebSocketMessageReassembler(4);
        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Binary, [1, 2, 3]));

        var step = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Continuation, [4, 5]));

        Assert.AreEqual(WebSocketReassemblyOutcome.MessageTooLarge, step.Outcome);
    }

    [TestMethod]
    public void Accept_JoinedPayloadExactlyAtTheMaximum_Completes()
    {
        var reassembler = new WebSocketMessageReassembler(4);
        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Binary, [1, 2, 3]));

        var step = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Continuation, [4]));

        Assert.AreEqual(WebSocketReassemblyOutcome.MessageComplete, step.Outcome);
    }

    [TestMethod]
    public void Accept_TextWithACodePointSplitAcrossFragments_IsAccepted()
    {
        var reassembler = new WebSocketMessageReassembler(0);

        // U+00E9 is C3 A9 in UTF-8, and U+1F600 is F0 9F 98 80; each is split between frames.
        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Text, [0x61, 0xC3]));
        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Continuation, [0xA9, 0xF0, 0x9F]));
        var step = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Continuation, [0x98, 0x80]));

        Assert.AreEqual(WebSocketReassemblyOutcome.MessageComplete, step.Outcome);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80 }, step.Message!.Payload);
    }

    [TestMethod]
    public void Accept_TextThatIsNotUtf8_IsTextNotUtf8()
    {
        var reassembler = new WebSocketMessageReassembler(0);
        reassembler.Accept(Frame(fin: false, WebSocketOpcode.Text, [0x61, 0xC3]));

        var step = reassembler.Accept(Frame(fin: true, WebSocketOpcode.Continuation, [0x28]));

        Assert.AreEqual(WebSocketReassemblyOutcome.TextNotUtf8, step.Outcome);
        Assert.IsNull(step.Message);
    }

    private static WebSocketFrame Frame(bool fin, WebSocketOpcode opcode, byte[] payload) =>
        new(fin, Rsv1: false, Rsv2: false, Rsv3: false, opcode, Masked: true, payload);
}
