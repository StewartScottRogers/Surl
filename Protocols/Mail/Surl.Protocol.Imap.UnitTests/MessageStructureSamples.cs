namespace Surl.Protocol.Imap;

/// <summary>
/// Messages the structure tests share, and what RFC 3501 section 7.4.2 makes of them.
/// </summary>
internal static class MessageStructureSamples
{
    /// <summary>
    /// A message whose inner message is <see cref="Multipart"/>'s part 2.
    /// </summary>
    public const string Inner =
        "Subject: inner\r\n"
        + "Content-Type: multipart/alternative; boundary=b2\r\n"
        + "\r\n"
        + "--b2\r\n"
        + "Content-Type: text/plain\r\n"
        + "\r\n"
        + "inner text\r\n"
        + "--b2   \r\n"
        + "Content-Type: text/html\r\n"
        + "Content-Transfer-Encoding: quoted-printable\r\n"
        + "\r\n"
        + "<p>x</p>\r\n"
        + "--b2--";

    /// <summary>
    /// <c>multipart/mixed</c>: a text part, then a <c>message/rfc822</c> attachment holding
    /// <see cref="Inner"/>, with a preamble and an epilogue.
    /// </summary>
    public const string Multipart =
        "From: \"Ann Example\" <ann@example.com>\r\n"
        + "To: bob@example.com, Team: carol@example.com, \"Dan, Jr.\" <dan@example.com>;\r\n"
        + "Cc: (nobody)\r\n"
        + "Subject: Report\r\n"
        + "Date: Tue, 29 Sep 2026 07:30:00 +0000\r\n"
        + "Message-ID: <m1@example.com>\r\n"
        + "MIME-Version: 1.0\r\n"
        + "Content-Type: multipart/mixed; boundary=\"b1\" (the boundary)\r\n"
        + "\r\n"
        + "preamble\r\n"
        + "--b1\r\n"
        + "Content-Type: text/plain; charset=utf-8\r\n"
        + "\r\n"
        + "hello\r\n"
        + "--b1\r\n"
        + "Content-Type: message/rfc822\r\n"
        + "Content-Disposition: attachment; filename=\"inner.eml\"\r\n"
        + "\r\n"
        + Inner + "\r\n"
        + "--b1--\r\n"
        + "epilogue\r\n";

    public const string InnerEnvelope = "(NIL \"inner\" NIL NIL NIL NIL NIL NIL NIL NIL)";

    public const string InnerBodyStructure =
        "((\"TEXT\" \"PLAIN\" NIL NIL NIL \"7BIT\" 10 1 NIL NIL NIL NIL)"
        + "(\"TEXT\" \"HTML\" NIL NIL NIL \"QUOTED-PRINTABLE\" 8 1 NIL NIL NIL NIL)"
        + " \"ALTERNATIVE\" (\"BOUNDARY\" \"b2\") NIL NIL NIL)";

    public const string MultipartBodyStructure =
        "((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"utf-8\") NIL NIL \"7BIT\" 5 1 NIL NIL NIL NIL)"
        + "(\"MESSAGE\" \"RFC822\" NIL NIL NIL \"7BIT\" 211 " + InnerEnvelope + " " + InnerBodyStructure + " 13 NIL (\"attachment\" (\"FILENAME\" \"inner.eml\")) NIL NIL)"
        + " \"MIXED\" (\"BOUNDARY\" \"b1\") NIL NIL NIL)";

    public const string MultipartBody =
        "((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"utf-8\") NIL NIL \"7BIT\" 5 1)"
        + "(\"MESSAGE\" \"RFC822\" NIL NIL NIL \"7BIT\" 211 " + InnerEnvelope
        + " ((\"TEXT\" \"PLAIN\" NIL NIL NIL \"7BIT\" 10 1)(\"TEXT\" \"HTML\" NIL NIL NIL \"QUOTED-PRINTABLE\" 8 1) \"ALTERNATIVE\") 13)"
        + " \"MIXED\")";

    public const string MultipartEnvelope =
        "(\"Tue, 29 Sep 2026 07:30:00 +0000\" \"Report\""
        + " ((\"Ann Example\" NIL \"ann\" \"example.com\"))"
        + " ((\"Ann Example\" NIL \"ann\" \"example.com\"))"
        + " ((\"Ann Example\" NIL \"ann\" \"example.com\"))"
        + " ((NIL NIL \"bob\" \"example.com\")(NIL NIL \"Team\" NIL)(NIL NIL \"carol\" \"example.com\")(\"Dan, Jr.\" NIL \"dan\" \"example.com\")(NIL NIL NIL NIL))"
        + " NIL NIL NIL \"<m1@example.com>\")";
}
