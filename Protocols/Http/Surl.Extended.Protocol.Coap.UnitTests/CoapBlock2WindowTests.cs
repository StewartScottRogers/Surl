using static Surl.Extended.Protocol.Coap.CoapTestExchange;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapBlock2Window"/>: which part of a representation a <c>2.05</c> carries (RFC 7959
/// sections 2.2 to 2.4 and 4; ADR-0097 decision 4), through <see cref="CoapResourceAnswerer"/>.
/// </summary>
[TestClass]
public sealed class CoapBlock2WindowTests
{
    [TestMethod]
    public async Task Select_ARepresentationOfExactlyTheBlockSize_GoesWholeWithoutBlock2()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("f", Body(1024)));

        var answer = await exchange.SendAsync(CoapCode.Get, "f");

        Assert.IsNull(BlockOf(answer, CoapOptionNumber.Block2));
        Assert.IsNull(UIntOf(answer, CoapOptionNumber.Size2));
        Assert.AreEqual(1024, answer.Payload.Length);
    }

    [TestMethod]
    public async Task Select_ARepresentationLongerThan1024Bytes_GoesIn1024ByteBlocks()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("f", Body(1025)));

        var first = await exchange.SendAsync(CoapCode.Get, "f");
        var second = await exchange.SendAsync(CoapCode.Get, "f", Block(CoapOptionNumber.Block2, "1/0/1024"));

        Assert.AreEqual("0/1/1024", BlockOf(first, CoapOptionNumber.Block2));
        Assert.AreEqual(1025u, UIntOf(first, CoapOptionNumber.Size2));
        Assert.AreEqual("1/0/1024", BlockOf(second, CoapOptionNumber.Block2));
        Assert.AreEqual("k", PayloadOf(second));
    }

    [TestMethod]
    public async Task Select_ABlockLargerThanTheServersSize_IsRenumberedInTheServersSize()
    {
        var body = Body(300);
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("f", body), preferredSizeExponent: 2);

        var answer = await exchange.SendAsync(CoapCode.Get, "f", Block(CoapOptionNumber.Block2, "1/1/128"));

        Assert.AreEqual("2/1/64", BlockOf(answer, CoapOptionNumber.Block2));
        Assert.AreEqual(body[128..192], PayloadOf(answer));
    }

    [TestMethod]
    public async Task Select_ABlockThatStartsPastTheEnd_Is402()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("f", Body(64)));

        var answer = await exchange.SendAsync(CoapCode.Get, "f", Block(CoapOptionNumber.Block2, "1/0/64"));

        Assert.AreEqual(CoapCode.BadOption, answer.Code);
    }

    [TestMethod]
    public async Task Select_Block0OfAnEmptyFile_IsTheOnlyBlockWithNoBytes()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("f", string.Empty));

        var answer = await exchange.SendAsync(CoapCode.Get, "f", Block(CoapOptionNumber.Block2, "0/0/16"));

        Assert.AreEqual(CoapCode.Content, answer.Code);
        Assert.AreEqual("0/0/16", BlockOf(answer, CoapOptionNumber.Block2));
        Assert.AreEqual(0u, UIntOf(answer, CoapOptionNumber.Size2));
        Assert.IsTrue(answer.Payload.IsEmpty);
    }

    [TestMethod]
    [DataRow(CoapOptionNumber.Block2)]
    [DataRow(CoapOptionNumber.Block1)]
    public async Task AnswerAsync_TheReservedSizeExponent7_Is400(int number)
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore(CoapTestStore.Uploads).WithFile("f", Body(64)));
        var method = number == CoapOptionNumber.Block2 ? CoapCode.Get : CoapCode.Put;

        var answer = await exchange.SendAsync(method, "f", CoapOption.FromUInt(number, 0x07));

        Assert.AreEqual(CoapCode.BadRequest, answer.Code);
    }

    [TestMethod]
    public async Task Select_WellKnownCore_IsServedInBlocksToo()
    {
        var store = new CoapTestStore(CoapTestStore.Listings).WithFile("a.txt", "1").WithFile("b.txt", "2");
        await using var exchange = new CoapTestExchange(store);
        const string core = ".well-known/core";

        var first = await exchange.SendAsync(CoapCode.Get, core, Block(CoapOptionNumber.Block2, "0/0/16"));
        var second = await exchange.SendAsync(CoapCode.Get, core, Block(CoapOptionNumber.Block2, "1/0/16"));
        var third = await exchange.SendAsync(CoapCode.Get, core, Block(CoapOptionNumber.Block2, "2/0/16"));

        Assert.AreEqual("</a.txt>;ct=0;sz=1;obs,</b.txt>;ct=0;sz=1;obs", PayloadOf(first) + PayloadOf(second) + PayloadOf(third));
        Assert.AreEqual("2/0/16", BlockOf(third, CoapOptionNumber.Block2));
    }

    [TestMethod]
    public async Task Select_AMatchingETag_Is203WithoutABlock()
    {
        await using var exchange = new CoapTestExchange(new CoapTestStore().WithFile("f", Body(2000)));

        var answer = await exchange.SendAsync(CoapCode.Get, "f", new CoapOption(CoapOptionNumber.ETag, CoapTestStore.EntityTagOf(2000)), Block(CoapOptionNumber.Block2, "1/0/1024"));

        Assert.AreEqual(CoapCode.Valid, answer.Code);
        Assert.IsNull(BlockOf(answer, CoapOptionNumber.Block2));
    }
}
