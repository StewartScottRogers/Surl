using static Surl.Hpack.Rfc7541Hex;

namespace Surl.Hpack;

/// <summary>
/// Every example of RFC 7541 Appendix C, decoded to the RFC's header lists and dynamic-table states
/// and encoded back to the RFC's bytes. The bytes are the RFC's (ADR-0003).
/// </summary>
[TestClass]
public sealed class HpackAppendixCTests
{
    private const int ResponseTableSize = 256;

    private const int AnyHeaderListSize = 1 << 20;

    private const string Date20 = "Mon, 21 Oct 2013 20:13:21 GMT";

    private const string Date22 = "Mon, 21 Oct 2013 20:13:22 GMT";

    private const string Location = "https://www.example.com";

    private const string SetCookie = "foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1";

    private static readonly HpackHeaderField[] Request1 =
    [
        new(":method", "GET"), new(":scheme", "http"), new(":path", "/"), new(":authority", "www.example.com"),
    ];

    private static readonly HpackHeaderField[] Request2 =
    [
        new(":method", "GET"), new(":scheme", "http"), new(":path", "/"), new(":authority", "www.example.com"),
        new("cache-control", "no-cache"),
    ];

    private static readonly HpackHeaderField[] Request3 =
    [
        new(":method", "GET"), new(":scheme", "https"), new(":path", "/index.html"),
        new(":authority", "www.example.com"), new("custom-key", "custom-value"),
    ];

    private static readonly HpackHeaderField[] Response1 =
    [
        new(":status", "302"), new("cache-control", "private"), new("date", Date20), new("location", Location),
    ];

    private static readonly HpackHeaderField[] Response2 =
    [
        new(":status", "307"), new("cache-control", "private"), new("date", Date20), new("location", Location),
    ];

    private static readonly HpackHeaderField[] Response3 =
    [
        new(":status", "200"), new("cache-control", "private"), new("date", Date22), new("location", Location),
        new("content-encoding", "gzip"), new("set-cookie", SetCookie),
    ];

    private static readonly HpackHeaderField[] Request1Table = [new(":authority", "www.example.com")];

    private static readonly HpackHeaderField[] Request2Table =
    [
        new("cache-control", "no-cache"), new(":authority", "www.example.com"),
    ];

    private static readonly HpackHeaderField[] Request3Table =
    [
        new("custom-key", "custom-value"), new("cache-control", "no-cache"), new(":authority", "www.example.com"),
    ];

    private static readonly HpackHeaderField[] Response1Table =
    [
        new("location", Location), new("date", Date20), new("cache-control", "private"), new(":status", "302"),
    ];

    private static readonly HpackHeaderField[] Response2Table =
    [
        new(":status", "307"), new("location", Location), new("date", Date20), new("cache-control", "private"),
    ];

    private static readonly HpackHeaderField[] Response3Table =
    [
        new("set-cookie", SetCookie), new("content-encoding", "gzip"), new("date", Date22),
    ];

    private static readonly string[] RequestsRaw =
    [
        "8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d",
        "8286 84be 5808 6e6f 2d63 6163 6865",
        "8287 85bf 400a 6375 7374 6f6d 2d6b 6579 0c63 7573 746f 6d2d 7661 6c75 65",
    ];

    private static readonly string[] RequestsHuffman =
    [
        "8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff",
        "8286 84be 5886 a8eb 1064 9cbf",
        "8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf",
    ];

    private static readonly string[] ResponsesRaw =
    [
        "4803 3330 3258 0770 7269 7661 7465 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a 3133 3a32 3120 474d"
            + " 546e 1768 7474 7073 3a2f 2f77 7777 2e65 7861 6d70 6c65 2e63 6f6d",
        "4803 3330 37c1 c0bf",
        "88c1 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a 3133 3a32 3220 474d 54c0 5a04 677a 6970 7738 666f"
            + " 6f3d 4153 444a 4b48 514b 425a 584f 5157 454f 5049 5541 5851 5745 4f49 553b 206d 6178 2d61 6765 3d33 3630"
            + " 303b 2076 6572 7369 6f6e 3d31",
    ];

    private static readonly string[] ResponsesHuffman =
    [
        "4882 6402 5885 aec3 771a 4b61 96d0 7abe 9410 54d4 44a8 2005 9504 0b81 66e0 82a6 2d1b ff6e 919d 29ad 1718 63c7"
            + " 8f0b 97c8 e9ae 82ae 43d3",
        "4883 640e ffc1 c0bf",
        "88c1 6196 d07a be94 1054 d444 a820 0595 040b 8166 e084 a62d 1bff c05a 839b d9ab 77ad 94e7 821d d7f2 e6c7 b335"
            + " dfdf cd5b 3960 d5af 2708 7f36 72c1 ab27 0fb5 291f 9587 3160 65c0 03ed 4ee5 b106 3d50 07",
    ];

    private static readonly HpackHeaderField[][] Requests = [Request1, Request2, Request3];

    private static readonly HpackHeaderField[][] RequestTables = [Request1Table, Request2Table, Request3Table];

    private static readonly int[] RequestTableSizes = [57, 110, 164];

    private static readonly HpackHeaderField[][] Responses = [Response1, Response2, Response3];

    private static readonly HpackHeaderField[][] ResponseTables = [Response1Table, Response2Table, Response3Table];

    private static readonly int[] ResponseTableSizes = [222, 222, 215];

    [TestMethod]
    [DataRow("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572", "custom-key", "custom-header", HpackFieldIndexing.IncrementalIndexing, 55)]
    [DataRow("040c 2f73 616d 706c 652f 7061 7468", ":path", "/sample/path", HpackFieldIndexing.WithoutIndexing, 0)]
    [DataRow("1008 7061 7373 776f 7264 0673 6563 7265 74", "password", "secret", HpackFieldIndexing.NeverIndexed, 0)]
    [DataRow("82", ":method", "GET", HpackFieldIndexing.IncrementalIndexing, 0)]
    public void Decode_AppendixC2_GivesTheFieldAndTheTableSize(
        string block, string name, string value, HpackFieldIndexing indexing, int tableSize)
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, AnyHeaderListSize);

        var result = decoder.Decode(Bytes(block));

        Assert.IsTrue(result.IsDecoded);
        CollectionAssert.AreEqual(new[] { new HpackHeaderField(name, value, indexing) }, result.Fields.ToArray());
        Assert.AreEqual(tableSize, decoder.TableSize);
    }

    [TestMethod]
    [DataRow("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572", "custom-key", "custom-header", HpackFieldIndexing.IncrementalIndexing)]
    [DataRow("040c 2f73 616d 706c 652f 7061 7468", ":path", "/sample/path", HpackFieldIndexing.WithoutIndexing)]
    [DataRow("1008 7061 7373 776f 7264 0673 6563 7265 74", "password", "secret", HpackFieldIndexing.NeverIndexed)]
    [DataRow("82", ":method", "GET", HpackFieldIndexing.IncrementalIndexing)]
    public void Encode_AppendixC2_GivesTheRfcBytes(string block, string name, string value, HpackFieldIndexing indexing)
    {
        var encoder = new HpackEncoder(HpackEncoder.DefaultMaximumTableSize, HpackStringCoding.Raw);

        var encoded = encoder.Encode([new HpackHeaderField(name, value, indexing)]);

        Assert.AreEqual(Hex(Bytes(block)), Hex(encoded));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Decode_AppendixC3AndC4Requests_GiveTheRfcHeaderListsAndTables(bool isHuffman)
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, AnyHeaderListSize);
        var blocks = isHuffman ? RequestsHuffman : RequestsRaw;

        for (var request = 0; request < blocks.Length; request++)
        {
            var result = decoder.Decode(Bytes(blocks[request]));

            CollectionAssert.AreEqual(Requests[request], result.Fields.ToArray(), $"request {request + 1}");
            CollectionAssert.AreEqual(RequestTables[request], decoder.DynamicTableEntries.ToArray(), $"request {request + 1}");
            Assert.AreEqual(RequestTableSizes[request], decoder.TableSize, $"request {request + 1}");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Encode_AppendixC3AndC4Requests_GiveTheRfcBytesAndTables(bool isHuffman)
    {
        var encoder = new HpackEncoder(
            HpackEncoder.DefaultMaximumTableSize, isHuffman ? HpackStringCoding.Huffman : HpackStringCoding.Raw);
        var blocks = isHuffman ? RequestsHuffman : RequestsRaw;

        for (var request = 0; request < blocks.Length; request++)
        {
            var encoded = encoder.Encode(Requests[request]);

            Assert.AreEqual(Hex(Bytes(blocks[request])), Hex(encoded), $"request {request + 1}");
            CollectionAssert.AreEqual(RequestTables[request], encoder.DynamicTableEntries.ToArray(), $"request {request + 1}");
            Assert.AreEqual(RequestTableSizes[request], encoder.TableSize, $"request {request + 1}");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Decode_AppendixC5AndC6Responses_GiveTheRfcHeaderListsAndTables(bool isHuffman)
    {
        var decoder = new HpackDecoder(ResponseTableSize, AnyHeaderListSize);
        var blocks = isHuffman ? ResponsesHuffman : ResponsesRaw;

        for (var response = 0; response < blocks.Length; response++)
        {
            var result = decoder.Decode(Bytes(blocks[response]));

            CollectionAssert.AreEqual(Responses[response], result.Fields.ToArray(), $"response {response + 1}");
            CollectionAssert.AreEqual(ResponseTables[response], decoder.DynamicTableEntries.ToArray(), $"response {response + 1}");
            Assert.AreEqual(ResponseTableSizes[response], decoder.TableSize, $"response {response + 1}");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Encode_AppendixC5AndC6Responses_GiveTheRfcBytesAndTables(bool isHuffman)
    {
        var encoder = new HpackEncoder(ResponseTableSize, isHuffman ? HpackStringCoding.Huffman : HpackStringCoding.Raw);
        var blocks = isHuffman ? ResponsesHuffman : ResponsesRaw;

        for (var response = 0; response < blocks.Length; response++)
        {
            var encoded = encoder.Encode(Responses[response]);

            Assert.AreEqual(Hex(Bytes(blocks[response])), Hex(encoded), $"response {response + 1}");
            CollectionAssert.AreEqual(ResponseTables[response], encoder.DynamicTableEntries.ToArray(), $"response {response + 1}");
            Assert.AreEqual(ResponseTableSizes[response], encoder.TableSize, $"response {response + 1}");
        }
    }
}
