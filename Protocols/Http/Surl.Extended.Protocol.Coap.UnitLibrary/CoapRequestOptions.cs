using System.Text;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The options of one request as the server reads them (RFC 7252 sections 5.4 and 5.10; ADR-0097
/// decision 3): the recognised ones collected, and the first unrecognised critical one named.
/// </summary>
/// <remarks>
/// An option is unrecognised when its number is not one the server knows, when it is not defined
/// for the request's method (Observe or a request ETag on anything but <c>GET</c>, Block1 on
/// <c>GET</c> or <c>DELETE</c>), when its length is outside its range (section 5.4.3), or when it
/// repeats an option that is not repeatable (section 5.4.5). An unrecognised elective option (an
/// even number) is ignored; an unrecognised critical one (an odd number, or one of the reserved
/// Location-* numbers 128, 132, 136 and 140, section 5.10.7) is <see cref="UnrecognisedCriticalOption"/>.
/// </remarks>
internal sealed class CoapRequestOptions
{
    private static readonly Dictionary<int, OptionRule> Rules = new()
    {
        [CoapOptionNumber.IfMatch] = new(true, 0, 8, AnyMethod),
        [CoapOptionNumber.UriHost] = new(false, 1, 255, AnyMethod),
        [CoapOptionNumber.ETag] = new(true, 1, 8, OnlyGet),
        [CoapOptionNumber.IfNoneMatch] = new(false, 0, 0, AnyMethod),
        [CoapOptionNumber.Observe] = new(false, 0, 3, OnlyGet),
        [CoapOptionNumber.UriPort] = new(false, 0, 2, AnyMethod),
        [CoapOptionNumber.UriPath] = new(true, 0, 255, AnyMethod),
        [CoapOptionNumber.ContentFormat] = new(false, 0, 2, AnyMethod),
        [CoapOptionNumber.UriQuery] = new(true, 0, 255, AnyMethod),
        [CoapOptionNumber.Accept] = new(false, 0, 2, AnyMethod),
        [CoapOptionNumber.Block2] = new(false, 0, 3, AnyMethod),
        [CoapOptionNumber.Block1] = new(false, 0, 3, code => code != CoapCode.Get && code != CoapCode.Delete),
        [CoapOptionNumber.Size2] = new(false, 0, 4, AnyMethod),
        [CoapOptionNumber.ProxyUri] = new(false, 1, 1034, AnyMethod),
        [CoapOptionNumber.ProxyScheme] = new(false, 1, 255, AnyMethod),
        [CoapOptionNumber.Size1] = new(false, 0, 4, AnyMethod),
    };

    // What each option the server acts on contributes; every other recognised option is read and ignored.
    private static readonly Dictionary<int, Action<CoapRequestOptions, CoapOption>> Collectors = new()
    {
        [CoapOptionNumber.UriPath] = (options, option) => options.uriPath.Add(Encoding.UTF8.GetString(option.Value.Span)),
        [CoapOptionNumber.UriQuery] = (options, option) => options.uriQuery.Add(Encoding.UTF8.GetString(option.Value.Span)),
        [CoapOptionNumber.IfMatch] = (options, option) => options.ifMatch.Add(option.Value),
        [CoapOptionNumber.ETag] = (options, option) => options.entityTags.Add(option.Value),
        [CoapOptionNumber.IfNoneMatch] = (options, _) => options.IfNoneMatch = true,
        [CoapOptionNumber.Accept] = (options, option) => options.Accept = option.ReadUInt(),
        [CoapOptionNumber.ProxyUri] = (options, _) => options.NamesAProxy = true,
        [CoapOptionNumber.ProxyScheme] = (options, _) => options.NamesAProxy = true,
        [CoapOptionNumber.ContentFormat] = (options, option) => options.ContentFormat = option.ReadUInt(),
        [CoapOptionNumber.Block1] = (options, option) => options.Block1 = CoapBlockOption.Read(option),
        [CoapOptionNumber.Block2] = (options, option) => options.Block2 = CoapBlockOption.Read(option),
        [CoapOptionNumber.Size1] = (options, option) => options.Size1 = option.ReadUInt(),
        [CoapOptionNumber.Size2] = (options, _) => options.AsksForSize2 = true,
    };

    // Location-* numbers RFC 7252 section 5.10.7 reserves: a request carrying one is 4.02, though they are even.
    private static readonly HashSet<int> ReservedLocationOptions = [128, 132, 136, 140];

    private readonly List<string> uriPath = [];
    private readonly List<string> uriQuery = [];
    private readonly List<ReadOnlyMemory<byte>> ifMatch = [];
    private readonly List<ReadOnlyMemory<byte>> entityTags = [];

    private CoapRequestOptions()
    {
    }

    /// <summary>The Uri-Path segments, decoded from UTF-8, in order.</summary>
    public IReadOnlyList<string> UriPath => uriPath;

    /// <summary>The Uri-Query arguments, decoded from UTF-8, in order.</summary>
    public IReadOnlyList<string> UriQuery => uriQuery;

    /// <summary>The If-Match values; an empty one matches any existing representation.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> IfMatch => ifMatch;

    /// <summary>Whether If-None-Match was sent.</summary>
    public bool IfNoneMatch { get; private set; }

    /// <summary>The ETags a <c>GET</c> offers for validation (section 5.10.6.2).</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> EntityTags => entityTags;

    /// <summary>The Accept format, when sent.</summary>
    public uint? Accept { get; private set; }

    /// <summary>Whether Proxy-Uri or Proxy-Scheme was sent.</summary>
    public bool NamesAProxy { get; private set; }

    /// <summary>The request's Content-Format, when sent.</summary>
    public uint? ContentFormat { get; private set; }

    /// <summary>The Block1 option: which block of a request body this is (RFC 7959 section 2.5).</summary>
    public CoapBlockOption? Block1 { get; private set; }

    /// <summary>The Block2 option: which block of the response the client asks for (RFC 7959 section 2.4).</summary>
    public CoapBlockOption? Block2 { get; private set; }

    /// <summary>The Size1 option: the size of the whole request body, when announced (RFC 7959 section 4).</summary>
    public uint? Size1 { get; private set; }

    /// <summary>Whether Size2 was sent, which asks for the representation's size (RFC 7959 section 4).</summary>
    public bool AsksForSize2 { get; private set; }
    /// <summary>The number of the first unrecognised critical option, when there is one.</summary>
    public int? UnrecognisedCriticalOption { get; private set; }

    /// <summary>
    /// Reads the options of <paramref name="request"/>.
    /// </summary>
    /// <param name="request">A request: its code is a method.</param>
    /// <returns>The options read.</returns>
    public static CoapRequestOptions Read(CoapMessage request)
    {
        var options = new CoapRequestOptions();
        var occurrences = new Dictionary<int, int>();

        foreach (var option in request.Options)
        {
            var occurrence = occurrences.GetValueOrDefault(option.Number) + 1;
            occurrences[option.Number] = occurrence;

            if (IsRecognised(option, occurrence, request.Code))
            {
                options.Collect(option);
            }
            else if (IsCriticalWhenUnrecognised(option.Number))
            {
                options.UnrecognisedCriticalOption ??= option.Number;
            }
        }

        return options;
    }

    private static bool IsRecognised(CoapOption option, int occurrence, CoapCode method) =>
        Rules.TryGetValue(option.Number, out var rule)
        && (occurrence == 1 || rule.IsRepeatable)
        && option.Value.Length >= rule.MinLength
        && option.Value.Length <= rule.MaxLength
        && rule.IsDefinedFor(method);

    private static bool IsCriticalWhenUnrecognised(int number) =>
        (number & 1) == 1 || ReservedLocationOptions.Contains(number);

    private void Collect(CoapOption option)
    {
        if (Collectors.TryGetValue(option.Number, out var collect))
        {
            collect(this, option);
        }
    }

    private static bool AnyMethod(CoapCode _) => true;

    private static bool OnlyGet(CoapCode method) => method == CoapCode.Get;

    private sealed record OptionRule(bool IsRepeatable, int MinLength, int MaxLength, Func<CoapCode, bool> IsDefinedFor);
}
