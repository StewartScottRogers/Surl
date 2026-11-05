using System.Text;
using System.Xml;
using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// Reads an XMPP stream from a connection incrementally (RFC 6120 sections 4 and 11; ADR-0114
/// decisions 0, 6 and 7): the stream header, then one first-level element at a time, however its
/// bytes are split across reads, until <c>&lt;/stream:stream&gt;</c> or the end of input.
/// </summary>
/// <remarks>
/// <para>
/// The BCL's <see cref="XmlReader"/> parses, with DTDs prohibited, no resolver, fragment
/// conformance, and comments and processing instructions reported so they can be refused. What
/// RFC 6120 section 11 forbids throws <see cref="XmppStreamErrorException"/>: a DTD, comment,
/// processing instruction or entity reference other than the five predefined is
/// <c>restricted-xml</c>; an encoding other than UTF-8 is <c>unsupported-encoding</c>; any other
/// parse failure is <c>not-well-formed</c>.
/// </para>
/// <para>
/// Limits (decision 7): an element over <c>--max-message</c> bytes, a value (attribute or text)
/// over it, nesting past <see cref="MaxDepth"/> levels or more than <see cref="MaxAttributes"/>
/// attributes on one element is <c>policy-violation</c>. Whitespace between first-level elements is
/// a keepalive and skipped (4.6.1); other text there is <c>policy-violation</c>.
/// </para>
/// </remarks>
internal sealed class XmppStreamReader
{
    /// <summary>The deepest a first-level element may nest, counting itself as level 1.</summary>
    public const int MaxDepth = 32;

    /// <summary>The most attributes, namespace declarations included, one element may carry.</summary>
    public const int MaxAttributes = 64;

    private static readonly XmlReaderSettings Settings = new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        ConformanceLevel = ConformanceLevel.Fragment,
        IgnoreComments = false,
        IgnoreProcessingInstructions = false,
        IgnoreWhitespace = false,
        CloseInput = false,
    };

    private readonly XmppInputStream input;
    private readonly XmlReader reader;
    private readonly long maxValueBytes;

    /// <summary>
    /// Creates a reader of a stream from its first byte.
    /// </summary>
    /// <param name="connection">The connection read.</param>
    /// <param name="maxElementBytes">The size bound on the header and each first-level element, and on any one value: <c>--max-message</c>.</param>
    public XmppStreamReader(IConnection connection, long maxElementBytes)
    {
        input = new XmppInputStream(connection, maxElementBytes);
        reader = XmlReader.Create(input, Settings);
        maxValueBytes = maxElementBytes;
    }

    /// <summary>
    /// How the last <see cref="ReadElementAsync"/> that returned <see langword="null"/> ended:
    /// <see langword="true"/> for the client's <c>&lt;/stream:stream&gt;</c>,
    /// <see langword="false"/> for the end of input.
    /// </summary>
    public bool ClientClosedStream { get; private set; }

    /// <summary>
    /// Reads the stream header: an optional XML declaration naming UTF-8, then
    /// <c>&lt;stream:stream&gt;</c> in <see cref="XmppNamespaces.Streams"/> with the default
    /// namespace <see cref="XmppNamespaces.Client"/>.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The header, or <see langword="null"/> when input ended before it.</returns>
    /// <exception cref="XmppStreamErrorException">The header is refused.</exception>
    public async Task<XmppStreamHeader?> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        while (await ReadNodeAsync(cancellationToken))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                return Header();
            }

            if (reader.NodeType == XmlNodeType.XmlDeclaration)
            {
                RequireUtf8Declaration();
            }
            else if (!IsWhitespace(reader.NodeType))
            {
                throw IsText(reader.NodeType)
                    ? new XmppStreamErrorException(XmppStreamErrors.NotWellFormed, "Text came before the stream header")
                    : Restricted(reader.NodeType);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the next first-level element whole, skipping whitespace keepalives.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>
    /// The element, or <see langword="null"/> when the stream ended, by the client's
    /// <c>&lt;/stream:stream&gt;</c> or the end of input (<see cref="ClientClosedStream"/> says which);
    /// an element cut off by the end of input is dropped.
    /// </returns>
    /// <exception cref="XmppStreamErrorException">The element or what came before it is refused.</exception>
    public async Task<XElement?> ReadElementAsync(CancellationToken cancellationToken)
    {
        while (await ReadNodeAsync(cancellationToken))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                input.StartElementCount();
                return await ReadWholeElementAsync(cancellationToken);
            }

            if (reader.NodeType == XmlNodeType.EndElement)
            {
                ClientClosedStream = true;
                return null;
            }

            if (!IsWhitespace(reader.NodeType))
            {
                throw IsText(reader.NodeType)
                    ? new XmppStreamErrorException(XmppStreamErrors.PolicyViolation, "Text came between first-level elements")
                    : Restricted(reader.NodeType);
            }
        }

        return null;
    }

    private static bool IsWhitespace(XmlNodeType nodeType) => nodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace;

    private static bool IsText(XmlNodeType nodeType) => nodeType is XmlNodeType.Text or XmlNodeType.CDATA;

    // One node; false at the end of input, including a cut-off element or header.
    private Task<bool> ReadNodeAsync(CancellationToken cancellationToken) =>
        ParsedAsync(reader.ReadAsync, valueAtEndOfInput: false, cancellationToken);

    // A text node's value is parsed only when asked for, so it can fail as a node can; at the end
    // of input the element it is in is cut off, and the empty value is never kept.
    private Task<string> TextValueAsync(CancellationToken cancellationToken) =>
        ParsedAsync(reader.GetValueAsync, valueAtEndOfInput: string.Empty, cancellationToken);

    private async Task<T> ParsedAsync<T>(Func<Task<T>> parse, T valueAtEndOfInput, CancellationToken cancellationToken)
    {
        input.ReadCancellation = cancellationToken;
        try
        {
            return await parse();
        }
        catch (XmlException) when (input.ReachedEnd)
        {
            return valueAtEndOfInput;
        }
        catch (XmlException failure)
        {
            throw Classified(failure);
        }
    }

    private void RequireUtf8Declaration()
    {
        var encoding = reader.GetAttribute("encoding");
        if (encoding is not null && !encoding.Equals("UTF-8", StringComparison.OrdinalIgnoreCase))
        {
            throw new XmppStreamErrorException(XmppStreamErrors.UnsupportedEncoding, $"The XML declaration named the encoding {encoding}");
        }
    }

    private XmppStreamHeader Header()
    {
        if (reader.LocalName != "stream")
        {
            throw new XmppStreamErrorException(XmppStreamErrors.NotWellFormed, "The first element was not a stream header");
        }

        if (reader.NamespaceURI != XmppNamespaces.Streams || reader.LookupNamespace(string.Empty) != XmppNamespaces.Client)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.InvalidNamespace, "The stream header's namespaces were not the client stream's");
        }

        input.StartElementCount();
        return new XmppStreamHeader(
            reader.GetAttribute("to"), reader.GetAttribute("version"), reader.GetAttribute("lang", XmppNamespaces.Xml), reader.IsEmptyElement);
    }

    private async Task<XElement?> ReadWholeElementAsync(CancellationToken cancellationToken)
    {
        var root = StartedElement(level: 1);
        var open = new Stack<XElement>();
        if (!reader.IsEmptyElement)
        {
            open.Push(root);
        }

        while (open.Count > 0)
        {
            if (!await ReadNodeAsync(cancellationToken))
            {
                return null;
            }

            await AddNodeAsync(open, cancellationToken);
        }

        return root;
    }

    private async Task AddNodeAsync(Stack<XElement> open, CancellationToken cancellationToken)
    {
        if (reader.NodeType == XmlNodeType.Element)
        {
            AddChildElement(open);
        }
        else if (reader.NodeType == XmlNodeType.EndElement)
        {
            open.Pop();
        }
        else if (IsText(reader.NodeType) || IsWhitespace(reader.NodeType))
        {
            open.Peek().Add(new XText(RequireValueWithinBound(await TextValueAsync(cancellationToken))));
        }
        else
        {
            throw Restricted(reader.NodeType);
        }
    }

    private void AddChildElement(Stack<XElement> open)
    {
        var child = StartedElement(open.Count + 1);
        open.Peek().Add(child);
        if (!reader.IsEmptyElement)
        {
            open.Push(child);
        }
    }

    private XElement StartedElement(int level)
    {
        if (level > MaxDepth)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.PolicyViolation, $"An element nested past {MaxDepth} levels");
        }

        if (reader.AttributeCount > MaxAttributes)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.PolicyViolation, $"An element carried more than {MaxAttributes} attributes");
        }

        var element = new XElement(XNamespace.Get(reader.NamespaceURI) + reader.LocalName);
        while (reader.MoveToNextAttribute())
        {
            element.Add(new XAttribute(AttributeName(), RequireValueWithinBound(reader.Value)));
        }

        reader.MoveToElement();
        return element;
    }

    // A namespace declaration keeps its xmlns form, so the element is written back as it came.
    private XName AttributeName() => reader.NamespaceURI switch
    {
        XmppNamespaces.XmlnsDeclarations when reader.Prefix.Length == 0 => "xmlns",
        XmppNamespaces.XmlnsDeclarations => XNamespace.Xmlns + reader.LocalName,
        _ => XNamespace.Get(reader.NamespaceURI) + reader.LocalName,
    };

    private string RequireValueWithinBound(string value)
    {
        if (Encoding.UTF8.GetByteCount(value) > maxValueBytes)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.PolicyViolation, $"A value passed the size bound of {maxValueBytes} bytes");
        }

        return value;
    }

    private static XmppStreamErrorException Restricted(XmlNodeType nodeType) =>
        new(XmppStreamErrors.RestrictedXml, $"The client sent a {nodeType}, which RFC 6120 section 11.1 forbids");

    private XmppStreamErrorException Classified(XmlException failure) =>
        new(XmppParseFailures.Condition(input.RecentText), $"The XML could not be parsed ({failure.Message})");
}
