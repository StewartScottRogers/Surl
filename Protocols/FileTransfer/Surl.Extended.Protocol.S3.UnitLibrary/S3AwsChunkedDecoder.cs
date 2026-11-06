using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The write-only stream an <c>aws-chunked</c> body is copied into ("Signature Calculations for
/// the Authorization Header: Transferring Payload in Multiple Chunks"; ADR-0108 decision 4.5):
/// it reads each chunk's <c>&lt;hex size&gt;[;chunk-signature=&lt;hex&gt;]</c> line, writes the
/// chunk's data to <paramref name="decodedSink"/>, checks a signed chunk's signature against
/// <paramref name="chunkSignatures"/>, and after the final empty chunk reads the trailer's
/// <c>name:value</c> lines up to the blank line. The first fault stops it: every later byte is
/// read and dropped, and <see cref="Finish"/> answers the fault.
/// </summary>
/// <param name="decodedSink">Where the chunks' data goes.</param>
/// <param name="chunkSignatures">The chain each chunk's signature is checked against; <see langword="null"/> for unsigned chunks, which carry none.</param>
/// <param name="decodedLength">The <c>x-amz-decoded-content-length</c>: the data's length, which it may not pass.</param>
internal sealed class S3AwsChunkedDecoder(Stream decodedSink, IHttpChunkSignatureChain? chunkSignatures, long decodedLength) : Stream
{
    /// <summary>
    /// The longest chunk header or trailer line read, its CRLF included.
    /// </summary>
    public const int MaxLineLength = 4096;

    private const string SignatureExtension = "chunk-signature=";

    private readonly List<byte> line = [];
    private readonly IncrementalHash chunkSha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private DecoderState state = DecoderState.ChunkHeader;
    private long chunkRemaining;
    private string? chunkSignature;
    private long decoded;
    private S3Error? fault;

    private enum DecoderState
    {
        ChunkHeader,
        ChunkData,
        ChunkEnd,
        Trailer,
        Done,
        Failed,
    }

    /// <summary>
    /// The trailer's fields by name, compared without regard to case.
    /// </summary>
    public Dictionary<string, string> Trailer { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// What the body came to once every byte is written: <see langword="null"/> when it ended with
    /// its trailer's blank line and its data is the decoded length; <c>403 SignatureDoesNotMatch</c>
    /// for a chunk whose signature did not verify; otherwise <c>400 IncompleteBody</c>.
    /// </summary>
    /// <returns>The error, or <see langword="null"/>.</returns>
    public S3Error? Finish() =>
        fault ?? (state == DecoderState.Done && decoded == decodedLength ? null : S3Error.IncompleteBody);

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Not supported: the body is copied asynchronously, never by a blocking write.
    /// </summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (!buffer.IsEmpty && state != DecoderState.Failed)
        {
            buffer = state switch
            {
                DecoderState.ChunkData => await WriteDataAsync(buffer, cancellationToken),
                DecoderState.Done => Fail(S3Error.IncompleteBody, buffer),
                _ => buffer[ReadLine(buffer.Span)..],
            };
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            chunkSha256.Dispose();
        }

        base.Dispose(disposing);
    }

    private ReadOnlyMemory<byte> Fail(S3Error error, ReadOnlyMemory<byte> rest)
    {
        fault = error;
        state = DecoderState.Failed;

        return rest;
    }

    // Takes bytes up to and including the next line feed, finishing the line when it is there.
    private int ReadLine(ReadOnlySpan<byte> bytes)
    {
        var lineFeed = bytes.IndexOf((byte)'\n');
        var taken = lineFeed < 0 ? bytes.Length : lineFeed + 1;
        line.AddRange(bytes[..taken]);
        if (line.Count > MaxLineLength)
        {
            Fail(S3Error.IncompleteBody, default);
        }
        else if (lineFeed >= 0)
        {
            FinishLine();
        }

        return taken;
    }

    private void FinishLine()
    {
        var text = Encoding.Latin1.GetString(CollectionsMarshal.AsSpan(line));
        line.Clear();
        if (!text.EndsWith("\r\n", StringComparison.Ordinal))
        {
            Fail(S3Error.IncompleteBody, default);
            return;
        }

        text = text[..^2];
        switch (state)
        {
            case DecoderState.ChunkHeader:
                ReadChunkHeader(text);
                break;
            case DecoderState.ChunkEnd:
                state = text.Length == 0 ? DecoderState.ChunkHeader : Failed();
                break;
            default:
                ReadTrailerLine(text);
                break;
        }
    }

    private DecoderState Failed()
    {
        fault = S3Error.IncompleteBody;

        return DecoderState.Failed;
    }

    // <hex size>, then ;chunk-signature=<hex> exactly when the chunks are signed; the data may not
    // pass the decoded length.
    private void ReadChunkHeader(string header)
    {
        var parts = header.Split(';', 2);
        var size = long.TryParse(parts[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
        var signature = SignatureOf(parts);
        if (size < 0 || (signature is null) != (chunkSignatures is null) || size > decodedLength - decoded)
        {
            Fail(S3Error.IncompleteBody, default);
            return;
        }

        chunkRemaining = size;
        chunkSignature = signature;
        if (size == 0)
        {
            FinishChunk(isFinal: true);
        }
        else
        {
            state = DecoderState.ChunkData;
        }
    }

    private static string? SignatureOf(string[] headerParts) =>
        headerParts.Length == 2 && headerParts[1].StartsWith(SignatureExtension, StringComparison.Ordinal)
            ? headerParts[1][SignatureExtension.Length..]
            : null;

    private async ValueTask<ReadOnlyMemory<byte>> WriteDataAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var data = buffer[..(int)Math.Min(chunkRemaining, buffer.Length)];
        chunkSha256.AppendData(data.Span);
        await decodedSink.WriteAsync(data, cancellationToken);
        decoded += data.Length;
        chunkRemaining -= data.Length;
        if (chunkRemaining == 0)
        {
            FinishChunk(isFinal: false);
        }

        return buffer[data.Length..];
    }

    // A signed chunk's signature is checked over its data's SHA-256; the final chunk is followed
    // by the trailer, any other by its CRLF.
    private void FinishChunk(bool isFinal)
    {
        var dataSha256 = chunkSha256.GetHashAndReset();
        if (chunkSignature is not null && !chunkSignatures!.VerifyNextChunk(dataSha256, chunkSignature))
        {
            Fail(S3Error.SignatureDoesNotMatch, default);
            return;
        }

        state = isFinal ? DecoderState.Trailer : DecoderState.ChunkEnd;
    }

    // The blank line ends the body; a signed body carries no trailer field, an unsigned one
    // name:value fields.
    private void ReadTrailerLine(string text)
    {
        if (text.Length == 0)
        {
            state = DecoderState.Done;
            return;
        }

        var colon = text.IndexOf(':');
        if (chunkSignatures is not null || colon <= 0)
        {
            Fail(S3Error.IncompleteBody, default);
            return;
        }

        Trailer[text[..colon].Trim()] = text[(colon + 1)..].Trim();
    }
}
