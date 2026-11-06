using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The additional checksums of "Checking object integrity" (ADR-0108 decision 4.5), each against
/// its catalogued check value over the nine bytes <c>123456789</c> (CRC-32/ISO-HDLC 0xCBF43926,
/// CRC-32C/ISCSI 0xE3069283) or its FIPS 180 digest.
/// </summary>
[TestClass]
public sealed class S3ChecksumTests
{
    [TestMethod]
    [DataRow("CRC32", "123456789", "CBF43926")]
    [DataRow("CRC32C", "123456789", "E3069283")]
    [DataRow("CRC32", "", "00000000")]
    [DataRow("SHA1", "abc", "A9993E364706816ABA3E25717850C26C9CD0D89D")]
    [DataRow("SHA256", "abc", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    public void Digest_KnownInput_IsItsCheckValueBigEndian(string algorithmName, string input, string digestHex)
    {
        using var checksum = new S3Checksum(S3ChecksumAlgorithm.Served.Single(algorithm => algorithm.Name == algorithmName));
        var bytes = Encoding.ASCII.GetBytes(input);

        checksum.Append(bytes.AsSpan(0, bytes.Length / 2));
        checksum.Append(bytes.AsSpan(bytes.Length / 2));

        Assert.AreEqual(digestHex, Convert.ToHexString(checksum.Digest()));
    }

    [TestMethod]
    [DataRow("x-amz-checksum-crc32", "CRC32")]
    [DataRow("X-Amz-Checksum-CRC32C", "CRC32C")]
    [DataRow("x-amz-checksum-sha1", "SHA1")]
    [DataRow("x-amz-checksum-sha256", "SHA256")]
    public void OfFieldName_AServedField_IsItsAlgorithm(string fieldName, string algorithmName)
    {
        Assert.AreEqual(algorithmName, S3ChecksumAlgorithm.OfFieldName(fieldName)?.Name);
    }

    [TestMethod]
    [DataRow("x-amz-checksum-crc64nvme")]
    [DataRow("x-amz-checksum-type")]
    public void OfFieldName_AnyOtherField_IsNone(string fieldName)
    {
        Assert.IsNull(S3ChecksumAlgorithm.OfFieldName(fieldName));
    }

    [TestMethod]
    public void Read_NoChecksumField_AsksForNone()
    {
        var request = S3ChecksumRequest.Read([new("x-amz-checksum-type", "FULL_OBJECT"), new("x-amz-sdk-checksum-algorithm", "CRC32")], S3PayloadForm.Plain, out var refusal);

        Assert.IsNull(request);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void Read_ATrailerAndAField_IsMultipleChecksums()
    {
        var request = S3ChecksumRequest.Read(
            [new("x-amz-trailer", "x-amz-checksum-crc32"), new("x-amz-checksum-sha1", "Kq5sNclPz7QV2+lfQIuc6R7oRu0=")],
            S3PayloadForm.UnsignedChunksWithTrailer,
            out var refusal);

        Assert.IsNull(request);
        Assert.AreSame(S3Error.MultipleChecksums, refusal);
    }

    [TestMethod]
    public void Read_ATrailerChecksum_HasNoSentDigest()
    {
        var request = S3ChecksumRequest.Read([new("x-amz-trailer", " x-amz-checksum-sha256 ")], S3PayloadForm.UnsignedChunksWithTrailer, out var refusal);

        Assert.AreEqual(S3ChecksumAlgorithm.Sha256, request?.Algorithm);
        Assert.IsNull(request?.SentDigest);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void Read_ATrailerOnSignedChunks_IsInvalidChecksum()
    {
        S3ChecksumRequest.Read([new("x-amz-trailer", "x-amz-checksum-crc32")], S3PayloadForm.SignedChunks, out var refusal);

        Assert.AreSame(S3Error.InvalidChecksum, refusal);
    }

    [TestMethod]
    public void ReceivedChecksum_Field_EchoesTheComputedDigest()
    {
        var received = new S3ReceivedChecksum(S3ChecksumAlgorithm.Crc32, [1, 2, 3, 4], [0x0D, 0x4A, 0x11, 0x85]);

        Assert.IsFalse(received.IsMatch);
        Assert.AreEqual(new KeyValuePair<string, string>("x-amz-checksum-crc32", "DUoRhQ=="), received.Field);
    }
}
