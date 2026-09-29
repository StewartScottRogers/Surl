using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Dict.DictTestExchange;

namespace Surl.Protocol.Dict;

/// <summary>
/// ADR-0006, section 3: a reply tells a peer no version and no local detail.
/// </summary>
[TestClass]
public sealed partial class WhatAPeerLearnsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Banner_HoldsNoVersionString()
    {
        var connection = new InMemoryConnection([]);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, exchangeId: 1234567));

        var banner = Utf8(connection.WrittenBytes);
        Assert.StartsWith("220 ", banner);
        Assert.DoesNotMatchRegex(VersionString(), banner);
    }

    [TestMethod]
    public async Task FailingDefinitionsSource_ReplyHoldsNoPathOrExceptionMessage()
    {
        const string exceptionMessage = "The disk said something private";
        var fileSystem = StandardFileSystem().AddUnreadableFile(Path.Join(Root, "vanishes"), new IOException(exceptionMessage));
        var connection = new InMemoryConnection(Ascii("DEFINE ! vanishes\r\nSTATUS\r\n"));

        await Server(fileSystem).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        var reply = Utf8(connection.WrittenBytes);
        Assert.StartsWith(Banner + "150 ", reply);
        Assert.DoesNotContain(Root, reply);
        Assert.DoesNotContain(exceptionMessage, reply);
        Assert.DoesNotContain(nameof(IOException), reply);
    }

    [GeneratedRegex(@"\d\.\d")]
    private static partial Regex VersionString();
}
