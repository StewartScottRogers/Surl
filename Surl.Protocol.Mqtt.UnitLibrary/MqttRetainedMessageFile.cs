using System.Buffers.Binary;
using System.Text;
using Surl.Content;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The file that keeps an <see cref="MqttRetainedMessages"/> store across restarts: read once
/// at start, and rewritten whole after every change, through <see cref="IContentFileSystem"/>
/// (ADR-0031, decision 6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Where.</b> <see cref="FileName"/> in the state folder it is given,
/// <c>&lt;data directory&gt;/.surl/mqtt</c> when surl serves with <c>--directory</c>. The
/// folder is created, with every missing folder above it, before each write.
/// </para>
/// <para>
/// <b>Bytes.</b> <see cref="Header"/>, then one record per topic in ordinal order of topic: the
/// topic's UTF-8 length as a 2-byte big-endian unsigned integer, its UTF-8 bytes, the
/// payload's length as a 4-byte big-endian unsigned integer, and the payload.
/// </para>
/// <para>
/// <b>How it is written.</b> To <c>.retained-messages-&lt;guid&gt;</c> beside the file, then
/// renamed over it, so a failure mid-write leaves the file as it was and never a partial one.
/// A write that throws deletes the temporary file before the exception is rethrown.
/// </para>
/// </remarks>
public sealed class MqttRetainedMessageFile
{
    /// <summary>
    /// The file's name in its state folder: <c>retained-messages</c>.
    /// </summary>
    public const string FileName = "retained-messages";

    /// <summary>
    /// The reason a file that does not parse is refused with: <c>not a retained-message file</c>.
    /// </summary>
    public const string MalformedFileReason = "not a retained-message file";

    private const string TemporaryFilePrefix = ".retained-messages-";
    private const int TopicLengthBytes = sizeof(ushort);
    private const int PayloadLengthBytes = sizeof(uint);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates the retained-message file in <paramref name="stateFolderPath"/>, read and
    /// written through <paramref name="fileSystem"/>. Nothing is read or written until asked.
    /// </summary>
    /// <param name="fileSystem">The seam every read and write goes through.</param>
    /// <param name="stateFolderPath">The full path of the folder the file is kept in.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stateFolderPath"/> is empty.</exception>
    public MqttRetainedMessageFile(IContentFileSystem fileSystem, string stateFolderPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(stateFolderPath);

        this.fileSystem = fileSystem;
        StateFolderPath = stateFolderPath;
        FilePath = Path.Join(stateFolderPath, FileName);
    }

    /// <summary>
    /// The 21 ASCII bytes every file starts with: <c>SURL-MQTT-RETAINED-1</c> and a line feed.
    /// </summary>
    public static ReadOnlySpan<byte> Header => "SURL-MQTT-RETAINED-1\n"u8;

    /// <summary>
    /// The full path of the folder the file is kept in.
    /// </summary>
    public string StateFolderPath { get; }

    /// <summary>
    /// The full path of the file: <see cref="FileName"/> in <see cref="StateFolderPath"/>.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Reads every topic and payload the file holds; none when there is no file.
    /// </summary>
    /// <param name="maxTopics">The most topics a valid file holds.</param>
    /// <param name="maxTotalPayloadBytes">The most payload bytes a valid file holds, all topics together.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each topic with its payload, in the order the file holds them.</returns>
    /// <exception cref="InvalidDataException">The file does not parse, or holds more than the
    /// bounds allow; its message is <see cref="MalformedFileReason"/>.</exception>
    internal async Task<IReadOnlyList<KeyValuePair<string, byte[]>>> ReadAsync(int maxTopics, long maxTotalPayloadBytes, CancellationToken cancellationToken)
    {
        if (fileSystem.GetEntryKind(FilePath) != ContentEntryKind.File)
        {
            return [];
        }

        var longestValidFile = Header.Length + ((long)maxTopics * (TopicLengthBytes + ushort.MaxValue + PayloadLengthBytes)) + maxTotalPayloadBytes;
        if (fileSystem.GetFileLength(FilePath) > longestValidFile)
        {
            throw Malformed();
        }

        var bytes = new MemoryStream();
        await using (var stream = fileSystem.OpenFileForAsyncRead(FilePath))
        {
            await stream.CopyToAsync(bytes, cancellationToken);
        }

        return Decode(bytes.ToArray(), maxTopics, maxTotalPayloadBytes);
    }

    /// <summary>
    /// Replaces the file with one holding exactly <paramref name="messages"/>, through a
    /// temporary file renamed into place.
    /// </summary>
    /// <param name="messages">Each topic with its payload.</param>
    /// <param name="cancellationToken">Cancels the write; the file is then left as it was.</param>
    /// <returns>A task that completes when the file is in place.</returns>
    internal async Task WriteAsync(IReadOnlyCollection<KeyValuePair<string, byte[]>> messages, CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(StateFolderPath);
        var temporaryPath = Path.Join(StateFolderPath, TemporaryFilePrefix + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var stream = fileSystem.CreateFileForAsyncWrite(temporaryPath))
            {
                await stream.WriteAsync(Encode(messages), cancellationToken);
            }

            fileSystem.MoveFileReplacing(temporaryPath, FilePath);
        }
        catch
        {
            fileSystem.DeleteFile(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// The bytes of a file holding <paramref name="messages"/>, in ordinal order of topic.
    /// </summary>
    /// <param name="messages">Each topic with its payload; every topic's UTF-8 form is at most
    /// 65535 bytes, as every valid topic name's is.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Encode(IEnumerable<KeyValuePair<string, byte[]>> messages)
    {
        var bytes = new MemoryStream();
        bytes.Write(Header);
        Span<byte> length = stackalloc byte[PayloadLengthBytes];
        foreach (var (topic, payload) in messages.OrderBy(message => message.Key, StringComparer.Ordinal))
        {
            var topicBytes = Encoding.UTF8.GetBytes(topic);
            BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)topicBytes.Length);
            bytes.Write(length[..TopicLengthBytes]);
            bytes.Write(topicBytes);
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)payload.Length);
            bytes.Write(length);
            bytes.Write(payload);
        }

        return bytes.ToArray();
    }

    private static List<KeyValuePair<string, byte[]>> Decode(byte[] bytes, int maxTopics, long maxTotalPayloadBytes)
    {
        if (!bytes.AsSpan().StartsWith(Header))
        {
            throw Malformed();
        }

        var messages = new List<KeyValuePair<string, byte[]>>();
        var topics = new HashSet<string>(StringComparer.Ordinal);
        var position = Header.Length;
        long totalPayloadBytes = 0;
        while (position < bytes.Length)
        {
            var (topic, payload) = DecodeRecord(bytes, ref position);
            totalPayloadBytes += payload.Length;
            if (!topics.Add(topic) || topics.Count > maxTopics || totalPayloadBytes > maxTotalPayloadBytes)
            {
                throw Malformed();
            }

            messages.Add(new(topic, payload));
        }

        return messages;
    }

    private static (string Topic, byte[] Payload) DecodeRecord(byte[] bytes, ref int position)
    {
        var topicLength = BinaryPrimitives.ReadUInt16BigEndian(Take(bytes, ref position, TopicLengthBytes));
        var topic = DecodeTopic(Take(bytes, ref position, topicLength));
        var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(Take(bytes, ref position, PayloadLengthBytes));
        if (payloadLength == 0 || payloadLength > int.MaxValue)
        {
            throw Malformed();
        }

        return (topic, Take(bytes, ref position, (int)payloadLength).ToArray());
    }

    private static string DecodeTopic(ReadOnlySpan<byte> topicBytes)
    {
        string topic;
        try
        {
            topic = StrictUtf8.GetString(topicBytes);
        }
        catch (DecoderFallbackException)
        {
            throw Malformed();
        }

        return MqttTopicFilter.IsValidTopicName(topic) ? topic : throw Malformed();
    }

    private static ReadOnlySpan<byte> Take(byte[] bytes, ref int position, int count)
    {
        if (bytes.Length - position < count)
        {
            throw Malformed();
        }

        var taken = bytes.AsSpan(position, count);
        position += count;
        return taken;
    }

    private static InvalidDataException Malformed() => new(MalformedFileReason);
}
