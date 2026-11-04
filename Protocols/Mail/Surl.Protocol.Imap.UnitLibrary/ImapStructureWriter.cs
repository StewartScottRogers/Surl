namespace Surl.Protocol.Imap;

/// <summary>
/// Writes a message's <c>ENVELOPE</c>, and its <c>BODY</c> or <c>BODYSTRUCTURE</c>, as RFC 3501
/// section 7.4.2 lays them out (ADR-0055, decision 4): strings quoted when they hold only
/// printable ASCII, literals otherwise, absent values <c>NIL</c>.
/// </summary>
internal static class ImapStructureWriter
{
    private static readonly string[] AddressFields = ["From", "Sender", "Reply-To", "To", "Cc", "Bcc"];

    /// <summary>
    /// Writes the envelope: date, subject, from, sender, reply-to, to, cc, bcc, in-reply-to and
    /// message-id; sender and reply-to are from's when they are absent or empty.
    /// </summary>
    /// <param name="writer">Where to write.</param>
    /// <param name="message">The message.</param>
    public static void WriteEnvelope(ImapDataWriter writer, ImapBodyPart message)
    {
        writer.Text("(").NString(FieldText(message, "Date")).Text(" ").NString(FieldText(message, "Subject"));
        var from = Addresses(message, "From");
        foreach (var field in AddressFields)
        {
            var addresses = Addresses(message, field);
            writer.Text(" ");
            WriteAddresses(writer, field is "Sender" or "Reply-To" && addresses.Count == 0 ? from : addresses);
        }

        writer.Text(" ").NString(FieldText(message, "In-Reply-To")).Text(" ").NString(FieldText(message, "Message-ID")).Text(")");
    }

    /// <summary>
    /// Writes the body structure: for a multipart its parts then its subtype, for any other part
    /// its type, subtype, parameters, id, description, encoding and size, then the lines of a
    /// text part, or the envelope, structure and lines of a <c>message/rfc822</c> part.
    /// </summary>
    /// <param name="writer">Where to write.</param>
    /// <param name="part">The message or part.</param>
    /// <param name="isExtended">Whether to write <c>BODYSTRUCTURE</c>'s extension data: a
    /// multipart's parameters, and a part's MD5; then each one's disposition, language and
    /// location.</param>
    public static void WriteBodyStructure(ImapDataWriter writer, ImapBodyPart part, bool isExtended)
    {
        writer.Text("(");
        if (part.Parts.Count > 0)
        {
            WriteMultipart(writer, part, isExtended);
        }
        else
        {
            WriteSinglePart(writer, part, isExtended);
        }

        writer.Text(")");
    }

    private static void WriteMultipart(ImapDataWriter writer, ImapBodyPart part, bool isExtended)
    {
        foreach (var child in part.Parts)
        {
            WriteBodyStructure(writer, child, isExtended);
        }

        writer.Text(" ").String(part.Type.Subtype);
        if (isExtended)
        {
            writer.Text(" ");
            WriteParameters(writer, part.Type.Parameters);
            WriteDispositionLanguageLocation(writer, part);
        }
    }

    private static void WriteSinglePart(ImapDataWriter writer, ImapBodyPart part, bool isExtended)
    {
        WriteBasicFields(writer, part);
        WriteHeldMessageAndLines(writer, part, isExtended);
        if (isExtended)
        {
            writer.Text(" ").NString(FieldText(part, "Content-MD5"));
            WriteDispositionLanguageLocation(writer, part);
        }
    }

    // Type, subtype, parameters, id, description, encoding and size.
    private static void WriteBasicFields(ImapDataWriter writer, ImapBodyPart part)
    {
        writer.String(part.Type.Type).Text(" ").String(part.Type.Subtype).Text(" ");
        WriteParameters(writer, part.Type.Parameters);
        writer.Text(" ").NString(FieldText(part, "Content-ID"))
            .Text(" ").NString(FieldText(part, "Content-Description"))
            .Text(" ").String(FieldText(part, "Content-Transfer-Encoding")?.ToUpperInvariant() ?? "7BIT")
            .Text(" ").Number(part.Body.Length);
    }

    // A message/rfc822 part's envelope and structure, then its lines; a text part's lines.
    private static void WriteHeldMessageAndLines(ImapDataWriter writer, ImapBodyPart part, bool isExtended)
    {
        if (part.Message is { } message)
        {
            writer.Text(" ");
            WriteEnvelope(writer, message);
            writer.Text(" ");
            WriteBodyStructure(writer, message, isExtended);
        }

        if (part.Message is not null || part.Type.IsText)
        {
            writer.Text(" ").Number(CountLines(part.Body.Span));
        }
    }

    private static void WriteDispositionLanguageLocation(ImapDataWriter writer, ImapBodyPart part)
    {
        writer.Text(" ");
        if (FieldText(part, "Content-Disposition") is { } disposition)
        {
            var value = ImapMimeValue.Parse(disposition);
            writer.Text("(").String(value.Value).Text(" ");
            WriteParameters(writer, value.Parameters);
            writer.Text(")");
        }
        else
        {
            writer.Text("NIL");
        }

        writer.Text(" ").NString(FieldText(part, "Content-Language")).Text(" ").NString(FieldText(part, "Content-Location"));
    }

    private static void WriteParameters(ImapDataWriter writer, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        if (parameters.Count == 0)
        {
            writer.Text("NIL");
            return;
        }

        var separator = "(";
        foreach (var parameter in parameters)
        {
            writer.Text(separator).String(parameter.Key).Text(" ").String(parameter.Value);
            separator = " ";
        }

        writer.Text(")");
    }

    private static void WriteAddresses(ImapDataWriter writer, IReadOnlyList<ImapAddress> addresses)
    {
        if (addresses.Count == 0)
        {
            writer.Text("NIL");
            return;
        }

        writer.Text("(");
        foreach (var address in addresses)
        {
            writer.Text("(").NString(address.Name).Text(" ").NString(address.Route).Text(" ")
                .NString(address.Mailbox).Text(" ").NString(address.Host).Text(")");
        }

        writer.Text(")");
    }

    private static IReadOnlyList<ImapAddress> Addresses(ImapBodyPart message, string name) =>
        FieldText(message, name) is { } text ? ImapAddress.ReadList(text) : [];

    // The first such field's unfolded value, or null when there is none.
    private static string? FieldText(ImapBodyPart part, string name) => ImapHeaderField.Find(part.Fields, name)?.Text;

    // The body's lines: each LF ends one, and bytes after the last LF make one more.
    private static long CountLines(ReadOnlySpan<byte> body) =>
        body.Count((byte)'\n') + (body.Length > 0 && body[^1] != '\n' ? 1 : 0);
}
