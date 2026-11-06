using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Reading a CompleteMultipartUpload body ("CompleteMultipartUpload", "Request body").
/// </summary>
[TestClass]
public sealed class S3CompleteMultipartUploadDocumentTests
{
    [TestMethod]
    public void Read_ThePagesExample_GivesEachPartInOrder()
    {
        var body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<CompleteMultipartUpload xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">\n"
            + "  <Part>\n    <PartNumber>1</PartNumber>\n    <ETag>\"a54357aff0632cce46d942af68356b38\"</ETag>\n  </Part>\n"
            + "  <Part>\n    <ChecksumCRC32>AAAAAA==</ChecksumCRC32>\n    <ETag>\"0c78aef83f66abc1fa1e8477f296d394\"</ETag>\n    <PartNumber>2</PartNumber>\n  </Part>\n"
            + "</CompleteMultipartUpload>\n";

        var parts = S3CompleteMultipartUploadDocument.Read(Encoding.UTF8.GetBytes(body));

        CollectionAssert.AreEqual(
            new[] { new S3CompletedPart(1, "\"a54357aff0632cce46d942af68356b38\""), new S3CompletedPart(2, "\"0c78aef83f66abc1fa1e8477f296d394\"") },
            parts);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not xml")]
    [DataRow("<Other><Part><PartNumber>1</PartNumber><ETag>e</ETag></Part></Other>")]
    [DataRow("<CompleteMultipartUpload/>")]
    [DataRow("<CompleteMultipartUpload></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload>text</CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Other/></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part/></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>1</PartNumber></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><ETag>e</ETag></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>one</PartNumber><ETag>e</ETag></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>-1</PartNumber><ETag>e</ETag></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><PartNumber>2</PartNumber><ETag>e</ETag></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>e</ETag><ETag>f</ETag></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>e</ETag>text</Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber><x/></PartNumber><ETag>e</ETag></Part></CompleteMultipartUpload>")]
    [DataRow("<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>e</ETag></Part></CompleteMultipartUpload><After/>")]
    [DataRow("<!DOCTYPE x [<!ENTITY e \"e\">]><CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>&e;</ETag></Part></CompleteMultipartUpload>")]
    public void Read_AMalformedBody_IsNull(string body)
    {
        var parts = S3CompleteMultipartUploadDocument.Read(Encoding.UTF8.GetBytes(body));

        Assert.IsNull(parts);
    }

    [TestMethod]
    public void Read_10000Parts_AreRead()
    {
        var parts = S3CompleteMultipartUploadDocument.Read(Body(S3CompleteMultipartUploadDocument.MaxParts));

        Assert.HasCount(S3CompleteMultipartUploadDocument.MaxParts, parts!);
    }

    [TestMethod]
    public void Read_10001Parts_IsNull()
    {
        var parts = S3CompleteMultipartUploadDocument.Read(Body(S3CompleteMultipartUploadDocument.MaxParts + 1));

        Assert.IsNull(parts);
    }

    private static byte[] Body(int partCount) =>
        Encoding.UTF8.GetBytes("<CompleteMultipartUpload>"
            + string.Concat(Enumerable.Range(1, partCount).Select(number => $"<Part><PartNumber>{number}</PartNumber><ETag>e</ETag></Part>"))
            + "</CompleteMultipartUpload>");
}
