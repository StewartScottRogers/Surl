using Surl.Content;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The file that keeps the AMQP 0-9-1 broker's durable declared exchanges and their bindings across
/// restarts, read and written only through <see cref="IContentFileSystem"/> (ADR-0112 decisions 5
/// and 6, with ADR-0031 decision 6's and ADR-0050 decision 7's rules).
/// </summary>
/// <remarks>
/// <para>
/// <b>Where.</b> <c>&lt;data directory&gt;/.surl/amqp0/exchanges</c>. The pre-declared
/// <c>amq.</c> exchanges and non-durable ones are not in it, and neither are <c>amq.topic</c>'s
/// bindings, which are the bus's.
/// </para>
/// <para>
/// <b>Bytes.</b> <see cref="Header"/>; the exchange count (a 4-byte big-endian long); then per
/// exchange in ordinal order of name: its name as a short string, its type octet (0 direct,
/// 1 fanout, 2 topic, 3 headers), its declared arguments as a field table, its binding count
/// (4 bytes), and per binding in ordinal order of queue, key and argument bytes: the queue and the
/// key as short strings and the arguments as a field table (empty but on a headers exchange).
/// Short strings and field tables are section 4.2.5's.
/// </para>
/// <para>
/// <b>How it is written.</b> Whole, to <c>.exchanges-&lt;guid&gt;</c> beside it, then renamed over
/// it, so a failed write leaves the file as it was. <b>How it is read.</b> Once, at load. A missing
/// file is empty; one that does not parse, names an exchange twice, an invalid or <c>amq.</c> name,
/// an unknown type, a headers binding with an invalid <c>x-match</c>, arguments on another type's
/// binding, or more exchanges or bindings than the broker's bounds, is refused with
/// <see cref="MalformedFileReason"/>.
/// </para>
/// </remarks>
public sealed class Amqp0ExchangeFiles
{
    /// <summary>The broker's state folder's name in <c>.surl</c>: <c>amqp0</c>.</summary>
    public const string StateFolderName = "amqp0";

    /// <summary>The exchange file's name: <c>exchanges</c>.</summary>
    public const string ExchangesFileName = "exchanges";

    /// <summary>The reason a file that does not parse, or holds more than the bounds allow, is refused with.</summary>
    public const string MalformedFileReason = "not an AMQP 0-9-1 exchange file";

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates the broker's files under <paramref name="surlFolderPath"/>, read and written through
    /// <paramref name="fileSystem"/>. Nothing is read or written until asked.
    /// </summary>
    /// <param name="fileSystem">The seam every read and write goes through.</param>
    /// <param name="surlFolderPath">The full path of the data directory's <c>.surl</c> folder.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="surlFolderPath"/> is empty.</exception>
    public Amqp0ExchangeFiles(IContentFileSystem fileSystem, string surlFolderPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(surlFolderPath);

        this.fileSystem = fileSystem;
        StateFolderPath = Path.Join(surlFolderPath, StateFolderName);
        ExchangesPath = Path.Join(StateFolderPath, ExchangesFileName);
    }

    /// <summary>The 23 ASCII bytes the file starts with: <c>SURL-AMQP0-EXCHANGES-1</c> and a line feed.</summary>
    public static ReadOnlySpan<byte> Header => "SURL-AMQP0-EXCHANGES-1\n"u8;

    /// <summary>The full path of the broker's state folder, <c>.surl/amqp0</c>.</summary>
    public string StateFolderPath { get; }

    /// <summary>The full path of the exchange file.</summary>
    public string ExchangesPath { get; }

    internal static byte[] Encode(IEnumerable<Amqp0PersistedExchange> exchanges)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteBytes(Header);
        var ordered = exchanges.OrderBy(exchange => exchange.Name, StringComparer.Ordinal).ToList();
        writer.WriteLong((uint)ordered.Count);
        foreach (var exchange in ordered)
        {
            WriteExchange(writer, exchange);
        }

        return writer.ToArray();
    }

    private static void WriteExchange(Amqp0WireWriter writer, Amqp0PersistedExchange exchange)
    {
        writer.WriteShortString(exchange.Name);
        writer.WriteOctet((byte)exchange.Type);
        writer.WriteBytes(exchange.Arguments);
        writer.WriteLong((uint)exchange.Bindings.Count);
        foreach (var binding in InOrdinalOrder(exchange.Bindings))
        {
            writer.WriteShortString(binding.Queue);
            writer.WriteShortString(binding.Key);
            writer.WriteBytes(binding.EncodedArguments);
        }
    }

    private static IOrderedEnumerable<Amqp0Binding> InOrdinalOrder(IReadOnlyList<Amqp0Binding> bindings) => bindings
        .OrderBy(binding => binding.Queue, StringComparer.Ordinal)
        .ThenBy(binding => binding.Key, StringComparer.Ordinal)
        .ThenBy(binding => Convert.ToHexString(binding.EncodedArguments), StringComparer.Ordinal);

    /// <summary>Reads the file: nothing when it is not there.</summary>
    /// <exception cref="Amqp0ExchangeLoadException">It cannot be read, does not parse, or passes a bound.</exception>
    internal async Task<List<Amqp0PersistedExchange>> ReadAsync(int maxExchanges, int maxBindings, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            if (fileSystem.GetEntryKind(ExchangesPath) != ContentEntryKind.File)
            {
                return [];
            }

            await using var stream = fileSystem.OpenFileForAsyncRead(ExchangesPath);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, cancellationToken);
            bytes = copy.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new Amqp0ExchangeLoadException(ExchangesPath, exception.Message, exception);
        }

        return Decode(bytes, maxExchanges, maxBindings)
            ?? throw new Amqp0ExchangeLoadException(ExchangesPath, MalformedFileReason);
    }

    /// <summary>Writes <paramref name="bytes"/> as the whole file; returns the failure's message, or <see langword="null"/>.</summary>
    internal async Task<string?> WriteAsync(byte[] bytes)
    {
        var temporaryPath = Path.Join(StateFolderPath, "." + ExchangesFileName + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            fileSystem.CreateDirectory(StateFolderPath);
            await using (var stream = fileSystem.CreateFileForAsyncWrite(temporaryPath))
            {
                await stream.WriteAsync(bytes);
            }

            fileSystem.MoveFileReplacing(temporaryPath, ExchangesPath);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(temporaryPath);
            return exception.Message;
        }
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            fileSystem.DeleteFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The temporary file stays; the next write uses another name.
        }
    }

    // The exchanges, or null when the bytes are not a well-formed file within the bounds.
    private static List<Amqp0PersistedExchange>? Decode(byte[] bytes, int maxExchanges, int maxBindings)
    {
        if (!bytes.AsSpan().StartsWith(Header))
        {
            return null;
        }

        try
        {
            var reader = new Amqp0WireReader(bytes.AsMemory(Header.Length));
            var count = reader.ReadLong();
            var exchanges = ReadExchanges(reader, Math.Min(count, (uint)maxExchanges));
            return exchanges is not null && IsWhole(exchanges, count, maxBindings, reader) ? exchanges : null;
        }
        catch (Amqp0SyntaxException)
        {
            return null;
        }
    }

    // Every exchange the count announced was read, within the bindings bound, and nothing follows.
    private static bool IsWhole(List<Amqp0PersistedExchange> exchanges, uint count, int maxBindings, Amqp0WireReader reader) =>
        exchanges.Count == count && exchanges.Sum(exchange => exchange.Bindings.Count) <= maxBindings && reader.Remaining == 0;

    // Up to count exchanges; null when one is invalid or named twice.
    private static List<Amqp0PersistedExchange>? ReadExchanges(Amqp0WireReader reader, uint count)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var exchanges = new List<Amqp0PersistedExchange>();
        for (var index = 0u; index < count; index++)
        {
            if (ReadExchange(reader) is not { } exchange || !names.Add(exchange.Name))
            {
                return null;
            }

            exchanges.Add(exchange);
        }

        return exchanges;
    }

    private static Amqp0PersistedExchange? ReadExchange(Amqp0WireReader reader)
    {
        var name = reader.ReadShortString();
        var type = (Amqp0ExchangeType)reader.ReadOctet();
        var arguments = Encoded(reader.ReadFieldTable());
        var bindingCount = reader.ReadLong();
        var bindings = new List<Amqp0Binding>();
        for (var index = 0u; index < bindingCount; index++)
        {
            bindings.Add(new(reader.ReadShortString(), reader.ReadShortString(), reader.ReadFieldTable()));
        }

        return IsValidName(name) && Enum.IsDefined(type) && bindings.TrueForAll(binding => IsValidBinding(type, binding))
            ? new(name, type, arguments, bindings)
            : null;
    }

    // A name a client could have declared: not empty, within the syntax, and not amq.
    private static bool IsValidName(string name) =>
        name.Length != 0 && Amqp0BrokerConnection.IsValidName(name) && !name.StartsWith("amq.", StringComparison.Ordinal);

    private static bool IsValidBinding(Amqp0ExchangeType type, Amqp0Binding binding) =>
        type == Amqp0ExchangeType.Headers ? Amqp0HeadersMatcher.IsValidBinding(binding.Arguments) : binding.Arguments.Fields.Count == 0;

    private static byte[] Encoded(Amqp0FieldTable table)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteFieldTable(table);
        return writer.ToArray();
    }
}
