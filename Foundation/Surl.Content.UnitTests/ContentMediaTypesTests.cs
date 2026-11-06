namespace Surl.Content;

[TestClass]
public sealed class ContentMediaTypesTests
{
    [TestMethod]
    [DataRow("avif", "image/avif")]
    [DataRow("bmp", "image/bmp")]
    [DataRow("css", "text/css")]
    [DataRow("csv", "text/csv")]
    [DataRow("gif", "image/gif")]
    [DataRow("gz", "application/gzip")]
    [DataRow("htm", "text/html")]
    [DataRow("html", "text/html")]
    [DataRow("ico", "image/vnd.microsoft.icon")]
    [DataRow("jpeg", "image/jpeg")]
    [DataRow("jpg", "image/jpeg")]
    [DataRow("js", "text/javascript")]
    [DataRow("json", "application/json")]
    [DataRow("md", "text/markdown")]
    [DataRow("mjs", "text/javascript")]
    [DataRow("mp3", "audio/mpeg")]
    [DataRow("mp4", "video/mp4")]
    [DataRow("oga", "audio/ogg")]
    [DataRow("ogg", "audio/ogg")]
    [DataRow("otf", "font/otf")]
    [DataRow("pdf", "application/pdf")]
    [DataRow("png", "image/png")]
    [DataRow("svg", "image/svg+xml")]
    [DataRow("tar", "application/x-tar")]
    [DataRow("ttf", "font/ttf")]
    [DataRow("txt", "text/plain")]
    [DataRow("wasm", "application/wasm")]
    [DataRow("wav", "audio/wav")]
    [DataRow("webm", "video/webm")]
    [DataRow("webp", "image/webp")]
    [DataRow("woff", "font/woff")]
    [DataRow("woff2", "font/woff2")]
    [DataRow("xml", "application/xml")]
    [DataRow("yaml", "application/yaml")]
    [DataRow("yml", "application/yaml")]
    [DataRow("zip", "application/zip")]
    public void Of_EveryExtensionInTheAdrsTable_NamesItsMediaTypeInAnyCase(string extension, string mediaType)
    {
        var lower = ContentMediaTypes.Of("file." + extension);
        var upper = ContentMediaTypes.Of("file." + extension.ToUpperInvariant());

        Assert.AreEqual(mediaType, lower);
        Assert.AreEqual(mediaType, upper);
        Assert.AreEqual(mediaType, ContentMediaTypes.Table[extension]);
    }

    [TestMethod]
    public void Table_HoldsExactlyTheAdrsThirtySixExtensions()
    {
        var count = ContentMediaTypes.Table.Count;

        Assert.AreEqual(36, count);
    }

    [TestMethod]
    [DataRow("noextension")]
    [DataRow("archive.unknownext")]
    [DataRow("trailing.")]
    [DataRow(".profile")]
    [DataRow(".png")]
    [DataRow("")]
    [DataRow("folder.png/")]
    [DataRow("folder.png/noextension")]
    [DataRow("folder.png\\noextension")]
    public void Of_UnknownOrMissingExtension_IsApplicationOctetStream(string path)
    {
        var mediaType = ContentMediaTypes.Of(path);

        Assert.AreEqual("application/octet-stream", mediaType);
        Assert.AreEqual(ContentMediaTypes.Unknown, mediaType);
    }

    [TestMethod]
    [DataRow("bucket/photos/a.png", "image/png")]
    [DataRow("archive.tar.gz", "application/gzip")]
    [DataRow("dir\\page.html", "text/html")]
    public void Of_Path_ReadsOnlyTheLastSegmentsLastExtension(string path, string mediaType)
    {
        var named = ContentMediaTypes.Of(path);

        Assert.AreEqual(mediaType, named);
    }

    [TestMethod]
    public void Of_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentMediaTypes.Of(null!));
    }
}
