using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The page the <c>httptls+srp</c> and <c>httptls+srp-ipv6</c> server halves answer every request
/// with, in the shape upstream's test server, <c>gnutls-serv --http</c>, writes its own page
/// (GnuTLS <c>src/serv.c</c>, <c>peer_print_info</c>; ADR-0122 decision 7): a case's
/// <c>&lt;reply&gt;</c> is not sent. Every line of the page starts with <c>&lt;</c> but the status
/// line, the header and the request's header fields, which are echoed after
/// <c>Your HTTP header was:&lt;PRE&gt;</c> on the same line, so test 320's <c>&lt;stripfile&gt;</c>
/// leaves the user line (as <c>FINE</c>) and every field after the first.
/// </summary>
public static class UpstreamTestCaseGnuTlsServPage
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// Composes the response, head and page, for one request; the connection closes after it.
    /// </summary>
    /// <param name="srpUserName">The user the TLS-SRP handshake authenticated.</param>
    /// <param name="headerFields">
    /// The request's header fields after its request line, in the order they came, each echoed as
    /// <c>name: value</c> and CRLF, then the empty line that ended the head.
    /// </param>
    /// <returns>The bytes to send, Latin-1.</returns>
    public static byte[] Compose(string srpUserName, IEnumerable<KeyValuePair<string, string>> headerFields)
    {
        ArgumentNullException.ThrowIfNull(srpUserName);
        ArgumentNullException.ThrowIfNull(headerFields);

        var page = new StringBuilder()
            .Append("HTTP/1.0 200 OK\r\n")
            .Append("Content-type: text/html\r\n")
            .Append("\r\n")
            .Append("<HTML><BODY>\n")
            .Append("<CENTER><H1>This is <a href=\"http://www.gnu.org/software/gnutls\">GnuTLS</a></H1></CENTER>\n")
            .Append("<p>Connected as user '").Append(srpUserName).Append("'.</p>\n")
            .Append("<hr><P>Your HTTP header was:<PRE>");
        foreach (var (name, value) in headerFields)
        {
            page.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        page.Append("\r\n")
            .Append("</PRE></P>\n")
            .Append("</BODY></HTML>\n");
        return Latin1.GetBytes(page.ToString());
    }
}
