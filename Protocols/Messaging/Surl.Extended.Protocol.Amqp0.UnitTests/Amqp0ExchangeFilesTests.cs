using Surl.Content;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>The broker's exchange file under <c>.surl/amqp0</c> (ADR-0112 decisions 5 and 6; BL-732).</summary>
[TestClass]
public sealed class Amqp0ExchangeFilesTests
{
    private static readonly byte[] EmptyTable = [0, 0, 0, 0];
    private static readonly byte[] MatchAnyTable = [0, 0, 0, 16, .. ShortString("x-match"), (byte)'S', 0, 0, 0, 3, (byte)'a', (byte)'n', (byte)'y'];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_SurlFolder_PutsTheFileInAmqp0()
    {
        var files = new Amqp0ExchangeFiles(new InMemoryContentFileSystem(new ManualTimeProvider()), SurlFolderPath);

        Assert.AreEqual(Path.Join(SurlFolderPath, "amqp0"), files.StateFolderPath);
        Assert.AreEqual(Path.Join(SurlFolderPath, "amqp0", "exchanges"), files.ExchangesPath);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp0ExchangeFiles(null!, SurlFolderPath));
        Assert.ThrowsExactly<ArgumentException>(() => new Amqp0ExchangeFiles(new InMemoryContentFileSystem(new ManualTimeProvider()), string.Empty));
    }

    [TestMethod]
    public void Encode_ExchangesAndBindings_AreInOrdinalOrder()
    {
        var anyBinding = new Amqp0Binding("b", string.Empty, new([new("x-match", Amqp0FieldValue.LongString("any"))]));
        var plainBinding = new Amqp0Binding("b", string.Empty, Amqp0FieldTable.Empty);
        var firstQueue = new Amqp0Binding("a", string.Empty, Amqp0FieldTable.Empty);

        var bytes = Amqp0ExchangeFiles.Encode(
        [
            new("y", Amqp0ExchangeType.Headers, EmptyTable, [anyBinding, plainBinding, firstQueue]),
            new("x", Amqp0ExchangeType.Direct, EmptyTable, [new("q", "k", Amqp0FieldTable.Empty)]),
        ]);

        byte[] expected =
        [
            .. "SURL-AMQP0-EXCHANGES-1\n"u8, 0, 0, 0, 2,
            .. ShortString("x"), 0, .. EmptyTable, 0, 0, 0, 1, .. ShortString("q"), .. ShortString("k"), .. EmptyTable,
            .. ShortString("y"), 3, .. EmptyTable, 0, 0, 0, 3,
            .. ShortString("a"), .. ShortString(string.Empty), .. EmptyTable,
            .. ShortString("b"), .. ShortString(string.Empty), .. EmptyTable,
            .. ShortString("b"), .. ShortString(string.Empty), .. MatchAnyTable,
        ];
        CollectionAssert.AreEqual(expected, bytes, Convert.ToHexString(bytes));
    }

    [TestMethod]
    public async Task ReadAsync_WhatEncodeWrote_IsTheSameExchanges()
    {
        var files = await FilesHoldingAsync(Amqp0ExchangeFiles.Encode([new("y", Amqp0ExchangeType.Headers, MatchAnyTable, [new("b", "k", new([new("x-match", Amqp0FieldValue.LongString("any"))]))])]));

        var exchange = (await ReadAsync(files)).Single();

        Assert.AreEqual("y", exchange.Name);
        Assert.AreEqual(Amqp0ExchangeType.Headers, exchange.Type);
        CollectionAssert.AreEqual(MatchAnyTable, exchange.Arguments);
        CollectionAssert.AreEqual(MatchAnyTable, exchange.Bindings.Single().EncodedArguments);
        Assert.AreEqual("k", exchange.Bindings.Single().Key);
    }

    [TestMethod]
    public async Task ReadAsync_NoFile_IsEmpty()
    {
        var files = new Amqp0ExchangeFiles(new InMemoryContentFileSystem(new ManualTimeProvider()), SurlFolderPath);

        Assert.IsEmpty(await ReadAsync(files));
    }

    [TestMethod]
    [DataRow("another header", DisplayName = "another header")]
    [DataRow("truncated", DisplayName = "an exchange count with no exchange")]
    [DataRow("trailing", DisplayName = "bytes after the last exchange")]
    [DataRow("empty name", DisplayName = "an empty name")]
    [DataRow("invalid name", DisplayName = "a name outside the syntax")]
    [DataRow("reserved name", DisplayName = "an amq. name")]
    [DataRow("unknown type", DisplayName = "a type octet past headers")]
    [DataRow("duplicate", DisplayName = "an exchange named twice")]
    [DataRow("bad x-match", DisplayName = "a headers binding with another x-match")]
    [DataRow("direct arguments", DisplayName = "arguments on a direct binding")]
    [DataRow("too many exchanges", DisplayName = "more exchanges than the bound")]
    [DataRow("too many bindings", DisplayName = "more bindings than the bound")]
    public async Task ReadAsync_MalformedFile_IsRefused(string fault)
    {
        var files = await FilesHoldingAsync(MalformedFile(fault));
        var maxExchanges = fault == "too many exchanges" ? 1 : 2;

        var failure = await Assert.ThrowsExactlyAsync<Amqp0ExchangeLoadException>(() => files.ReadAsync(maxExchanges, 1, TestContext.CancellationToken));

        Assert.AreEqual(Amqp0ExchangeFiles.MalformedFileReason, failure.Message);
        Assert.AreEqual(files.ExchangesPath, failure.FilePath);
    }

    [TestMethod]
    public async Task WriteAsync_WriteFails_DeletesTheTemporaryFileAndReturnsTheMessage()
    {
        var fileSystem = new UnitTestFailingContentFileSystem { FailWrite = true };
        var files = new Amqp0ExchangeFiles(fileSystem, SurlFolderPath);

        var failure = await files.WriteAsync(Amqp0ExchangeFiles.Encode([]));

        Assert.AreEqual("The write failed.", failure);
        Assert.StartsWith(Path.Join(files.StateFolderPath, ".exchanges-"), fileSystem.DeletedFiles.Single());
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(files.ExchangesPath));
    }

    private static string SurlFolderPath => Path.Join(InMemoryContentFileSystem.RootPath, ".surl");

    private static byte[] MalformedFile(string fault) => fault switch
    {
        "another header" => "SURL-AMQP0-EXCHANGES-2\n"u8.ToArray(),
        "truncated" => [.. Amqp0ExchangeFiles.Header, 0, 0, 0, 1],
        "trailing" => [.. Amqp0ExchangeFiles.Header, 0, 0, 0, 0, 1],
        "empty name" => File(Exchange(string.Empty, 0)),
        "invalid name" => File(Exchange("bad name", 0)),
        "reserved name" => File(Exchange("amq.x", 0)),
        "unknown type" => File(Exchange("x", 4)),
        "duplicate" => File(Exchange("x", 0), Exchange("x", 0)),
        "bad x-match" => File(Exchange("x", 3, [.. ShortString("q"), .. ShortString(string.Empty), 0, 0, 0, 17, .. ShortString("x-match"), (byte)'S', 0, 0, 0, 4, .. "some"u8])),
        "direct arguments" => File(Exchange("x", 0, [.. ShortString("q"), .. ShortString("k"), .. MatchAnyTable])),
        "too many exchanges" => File(Exchange("x", 0), Exchange("y", 0)),
        _ => File(Exchange("x", 0, [.. ShortString("q"), .. ShortString("a"), .. EmptyTable], [.. ShortString("q"), .. ShortString("b"), .. EmptyTable])),
    };

    private static byte[] File(params byte[][] exchanges) => [.. Amqp0ExchangeFiles.Header, 0, 0, 0, (byte)exchanges.Length, .. exchanges.SelectMany(exchange => exchange)];

    private static byte[] Exchange(string name, byte type, params byte[][] bindings) =>
        [.. ShortString(name), type, .. EmptyTable, 0, 0, 0, (byte)bindings.Length, .. bindings.SelectMany(binding => binding)];

    private async Task<Amqp0ExchangeFiles> FilesHoldingAsync(byte[] bytes)
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var files = new Amqp0ExchangeFiles(fileSystem, SurlFolderPath);
        fileSystem.CreateDirectory(files.StateFolderPath);
        await using (var stream = fileSystem.CreateFileForAsyncWrite(files.ExchangesPath))
        {
            await stream.WriteAsync(bytes, TestContext.CancellationToken);
        }

        return files;
    }

    private Task<List<Amqp0PersistedExchange>> ReadAsync(Amqp0ExchangeFiles files) =>
        files.ReadAsync(Amqp0ExchangeTable.MaxDeclaredExchanges, Amqp0ExchangeTable.MaxBindings, TestContext.CancellationToken);
}
