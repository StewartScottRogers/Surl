using Surl.Core;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The request ID an Azure Storage reply carries.
/// </summary>
[TestClass]
public sealed class AzureReplyTests
{
    [TestMethod]
    public void RequestId_SameSeededIdentifiers_IsTheSameEveryRun()
    {
        var first = AzureReply.NewRequestId(new SeededIdentifierRandom(7).ForExchange(1));
        var second = AzureReply.NewRequestId(new SeededIdentifierRandom(7).ForExchange(1));
        var other = AzureReply.NewRequestId(new SeededIdentifierRandom(8).ForExchange(1));

        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, other);
        Assert.IsTrue(Guid.TryParseExact(first, "D", out _));
    }

    [TestMethod]
    public async Task RequestId_AnAnsweredRequestOnASeededExchange_IsTheSameEveryRun()
    {
        var first = new AzureServerHarness { Seed = 7 };
        var second = new AzureServerHarness { Seed = 7 };

        var firstResponse = await first.SendWithBearerAsync("PATCH", "/photos/a.txt");
        var secondResponse = await second.SendWithBearerAsync("PATCH", "/photos/a.txt");

        Assert.AreEqual(AzureServerHarness.RequestIdOf(firstResponse), AzureServerHarness.RequestIdOf(secondResponse));
    }

    [TestMethod]
    public async Task RequestId_AHeadPastTheLimitOnASeededExchange_IsTheSameEveryRun()
    {
        var first = new AzureServerHarness { Seed = 7 };
        var second = new AzureServerHarness { Seed = 7 };
        var limits = Surl.Protocol.Abstractions.ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };
        var request = $"GET /photos/a.txt HTTP/1.1\r\nHost: account.blob.core.windows.net\r\nX-Padding: {new string('p', 100)}\r\n\r\n";

        var firstResponse = await first.SendRawAsync(request, limits);
        var secondResponse = await second.SendRawAsync(request, limits);

        Assert.AreEqual(AzureServerHarness.RequestIdOf(firstResponse), AzureServerHarness.RequestIdOf(secondResponse));
    }
}
