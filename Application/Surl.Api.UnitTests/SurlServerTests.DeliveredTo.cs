using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1503: with MailDeliveredTo on, Mail.DeliverAsync prepends one "Delivered-To: <address>" line (ADR-0053
// amendment 1 decision 6); off, the stored bytes are the handed message.
public sealed partial class SurlServerTests
{
    private static SurlServerOptions DeliveredToOptions(bool on)
    {
        var options = StoreOptions(Http);
        options.RecordExchanges = true;
        options.MailDeliveredTo = on;
        return options;
    }

    private async Task<byte[]> DeliverAndReadFirstAsync(SurlServerOptions options)
    {
        await using var server = await SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;
        await server.Mail.DeliverAsync("alice@x", Encoding.ASCII.GetBytes(SeededMail), token);
        var page = await server.Mail.ListMessagesAsync("alice", "INBOX", 0, 100, token);
        return (await server.Mail.ReadMessageAsync("alice", "INBOX", page.Messages[0].Uid, token)).Bytes.ToArray();
    }

    [TestMethod]
    public async Task MailDeliverAsync_DeliveredToOn_StoresOneDeliveredToLineThenTheMessage()
    {
        var stored = await DeliverAndReadFirstAsync(DeliveredToOptions(on: true));

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("Delivered-To: alice@x\r\n" + SeededMail), stored);
    }

    [TestMethod]
    public async Task MailDeliverAsync_DeliveredToOff_StoresTheMessageAsHanded()
    {
        var stored = await DeliverAndReadFirstAsync(DeliveredToOptions(on: false));

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(SeededMail), stored);
    }

    [TestMethod]
    public async Task MailDeliverAsync_TheStoreChangedEvent_NamesTheAddressInDeliveredFor()
    {
        await using var server = await SurlServer.StartAsync(DeliveredToOptions(on: true), new ApiTestSeams().Host, TestContext.CancellationToken);
        await server.Mail.DeliverAsync("alice@x", Encoding.ASCII.GetBytes(SeededMail), TestContext.CancellationToken);

        var change = await WaitForStoreChangeAsync(server, StoreKind.Mail);

        CollectionAssert.AreEqual(new[] { "alice@x" }, change.DeliveredFor!.ToArray());
    }
}
